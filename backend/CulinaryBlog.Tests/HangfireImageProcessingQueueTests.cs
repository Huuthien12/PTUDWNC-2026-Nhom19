using CulinaryBlog.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.States;
using Hangfire.Storage;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class HangfireImageProcessingQueueTests
{
    [Theory]
    [InlineData("Enqueued")]
    [InlineData("Processing")]
    [InlineData("Scheduled")]
    [InlineData("Awaiting")]
    public void Active_retrying_or_unknown_job_is_not_queued_again(string state)
    {
        var fixture = new QueueFixture();
        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);
        fixture.Store.SetState(fixture.ImageId, state);

        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);

        Assert.Equal(1, fixture.Client.CreatedCount);
    }

    [Theory]
    [InlineData("Succeeded")]
    [InlineData("Failed")]
    [InlineData("Deleted")]
    public void Finished_or_deleted_job_is_recovered(string state)
    {
        var fixture = new QueueFixture();
        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);
        fixture.Store.SetState(fixture.ImageId, state);

        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);

        Assert.Equal(2, fixture.Client.CreatedCount);
        Assert.Equal(2, fixture.Client.RunnableCount);
    }

    [Fact]
    public void Missing_job_with_a_stale_association_is_recovered()
    {
        var fixture = new QueueFixture();
        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);
        fixture.Store.SetState(fixture.ImageId, null);
        fixture.Client.MarkCurrentJobLost();

        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);

        Assert.Equal(2, fixture.Client.CreatedCount);
        Assert.Equal(1, fixture.Client.RunnableCount);
    }

    [Fact]
    public async Task Competing_recovery_workers_create_one_job_per_image()
    {
        var fixture = new QueueFixture();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey))));

        Assert.Equal(1, fixture.Client.CreatedCount);
        Assert.Equal(1, fixture.Client.RunnableCount);
    }

    [Theory]
    [InlineData(CrashPoint.BeforeCreate, 1, 1)]
    [InlineData(CrashPoint.AfterJobRecord, 2, 1)]
    [InlineData(CrashPoint.AfterAssociationCommit, 1, 1)]
    public void Recovery_handles_each_creation_crash_window(CrashPoint crashPoint,
        int expectedCreated, int expectedRunnable)
    {
        var fixture = new QueueFixture();
        fixture.Client.CrashOnceAt = crashPoint;

        Assert.Throws<SimulatedCrashException>(() =>
            fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey));

        fixture.Queue.EnqueueProcess(fixture.RecipeId, fixture.ImageId, fixture.ObjectKey);

        Assert.Equal(expectedCreated, fixture.Client.CreatedCount);
        Assert.Equal(expectedRunnable, fixture.Client.RunnableCount);
    }

    [Fact]
    public void Association_is_written_through_the_state_transaction()
    {
        var transaction = new RecordingTransaction();
        var imageId = Guid.NewGuid();

        ImageProcessingJobAssociation.Track(transaction, imageId, "42");

        var entry = Assert.Single(transaction.HashWrites);
        Assert.Equal(ImageProcessingJobAssociation.Key(imageId), entry.Key);
        Assert.Equal("42", Assert.Single(entry.Value).Value);
    }

    [Fact]
    public void Processing_method_has_the_atomic_association_filter()
    {
        var method = typeof(RecipeImageProcessingJob).GetMethod(nameof(RecipeImageProcessingJob.ProcessAsync));

        Assert.NotNull(method);
        Assert.Contains(method.GetCustomAttributes(inherit: true), attribute =>
            attribute is TrackImageProcessingJobAttribute);
    }

    [PostgresFact]
    public async Task PostgreSql_storage_coordinates_competing_clients_and_publishes_the_association()
    {
        var connectionString = Environment.GetEnvironmentVariable("RECIPE_TEST_POSTGRES")!;
        var options = new PostgreSqlStorageOptions
        {
            SchemaName = $"hf_p1_{Guid.NewGuid():N}"
        };
        var storage = new PostgreSqlStorage(new NpgsqlConnectionFactory(connectionString, options), options);
        var imageId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var queues = Enumerable.Range(0, 12)
            .Select(_ => new HangfireImageProcessingQueue(new BackgroundJobClient(storage), storage))
            .ToArray();

        await Task.WhenAll(queues.Select(queue => Task.Run(() =>
            queue.EnqueueProcess(recipeId, imageId, $"recipes/{recipeId:D}/original.jpg"))));

        using var connection = storage.GetConnection();
        var association = connection.GetAllEntriesFromHash(ImageProcessingJobAssociation.Key(imageId));
        Assert.NotNull(association);
        var jobId = association[ImageProcessingJobAssociation.JobIdField];
        Assert.Equal(EnqueuedState.StateName, connection.GetJobData(jobId)?.State);
    }

    private sealed class QueueFixture
    {
        public Guid RecipeId { get; } = Guid.NewGuid();
        public Guid ImageId { get; } = Guid.NewGuid();
        public string ObjectKey => $"recipes/{RecipeId:D}/original.jpg";
        public MemoryDeduplicationStore Store { get; } = new();
        public RecordingBackgroundJobClient Client { get; }
        public HangfireImageProcessingQueue Queue { get; }

        public QueueFixture()
        {
            Client = new RecordingBackgroundJobClient(Store, ImageId);
            Queue = new HangfireImageProcessingQueue(Client, Store);
        }
    }

    public enum CrashPoint
    {
        None,
        BeforeCreate,
        AfterJobRecord,
        AfterAssociationCommit
    }

    private sealed class SimulatedCrashException : Exception;

    private sealed class RecordingBackgroundJobClient(MemoryDeduplicationStore store, Guid imageId)
        : IBackgroundJobClient
    {
        private int _createdCount;
        private int _runnableCount;
        public int CreatedCount => _createdCount;
        public int RunnableCount => _runnableCount;
        public CrashPoint CrashOnceAt { get; set; }

        public void MarkCurrentJobLost() => Interlocked.Decrement(ref _runnableCount);

        public string Create(Job job, IState state)
        {
            var crash = CrashOnceAt;
            CrashOnceAt = CrashPoint.None;
            if (crash == CrashPoint.BeforeCreate) throw new SimulatedCrashException();

            var jobId = Interlocked.Increment(ref _createdCount).ToString();
            if (crash == CrashPoint.AfterJobRecord) throw new SimulatedCrashException();

            store.Track(imageId, jobId, state.Name);
            Interlocked.Increment(ref _runnableCount);
            if (crash == CrashPoint.AfterAssociationCommit) throw new SimulatedCrashException();
            return jobId;
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => false;
    }

    private sealed class MemoryDeduplicationStore : IImageProcessingDeduplicationStore
    {
        private readonly Dictionary<Guid, Entry> _entries = [];
        private readonly Dictionary<Guid, object> _locks = [];
        private readonly object _gate = new();

        public IImageProcessingReservation Acquire(Guid imageId)
        {
            object imageLock;
            lock (_gate)
            {
                if (!_locks.TryGetValue(imageId, out imageLock!)) _locks[imageId] = imageLock = new();
            }
            Monitor.Enter(imageLock);
            return new Reservation(this, imageId, imageLock);
        }

        public void Track(Guid imageId, string jobId, string state)
        {
            lock (_gate) _entries[imageId] = new(jobId, state);
        }

        public void SetState(Guid imageId, string? state)
        {
            lock (_gate)
            {
                var current = _entries[imageId];
                _entries[imageId] = current with { State = state };
            }
        }

        private sealed class Reservation(MemoryDeduplicationStore owner, Guid imageId, object imageLock)
            : IImageProcessingReservation
        {
            public bool CanEnqueue()
            {
                lock (owner._gate)
                    return !owner._entries.TryGetValue(imageId, out var entry) ||
                        ImageProcessingDeduplication.CanEnqueue(entry.State);
            }

            public void Dispose() => Monitor.Exit(imageLock);
        }

        private sealed record Entry(string JobId, string? State);
    }

    private sealed class RecordingTransaction : IWriteOnlyTransaction
    {
        public List<(string Key, IReadOnlyList<KeyValuePair<string, string>> Value)> HashWrites { get; } = [];
        public void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs) =>
            HashWrites.Add((key, keyValuePairs.ToList()));
        public void Dispose() { }
        public void Commit() { }
        public void ExpireJob(string jobId, TimeSpan expireIn) { }
        public void PersistJob(string jobId) { }
        public void SetJobState(string jobId, IState state) { }
        public void AddJobState(string jobId, IState state) { }
        public void AddToQueue(string queue, string jobId) { }
        public void IncrementCounter(string key) { }
        public void IncrementCounter(string key, TimeSpan expireIn) { }
        public void DecrementCounter(string key) { }
        public void DecrementCounter(string key, TimeSpan expireIn) { }
        public void AddToSet(string key, string value) { }
        public void AddToSet(string key, string value, double score) { }
        public void RemoveFromSet(string key, string value) { }
        public void InsertToList(string key, string value) { }
        public void RemoveFromList(string key, string value) { }
        public void TrimList(string key, int keepStartingFrom, int keepEndingAt) { }
        public void RemoveHash(string key) { }
    }
}
