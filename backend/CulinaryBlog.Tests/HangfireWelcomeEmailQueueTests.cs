using CulinaryBlog.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class HangfireWelcomeEmailQueueTests
{
    [Theory]
    [InlineData("Enqueued")]
    [InlineData("Processing")]
    [InlineData("Scheduled")]
    [InlineData("Awaiting")]
    [InlineData("Succeeded")]
    public async Task Duplicate_or_completed_welcome_email_is_not_queued(string state)
    {
        var fixture = new QueueFixture();

        await fixture.Queue.EnqueueAsync("User@Example.test", "User");
        fixture.Store.SetState("user@example.test", state);

        await fixture.Queue.EnqueueAsync("user@example.test", "User");

        Assert.Equal(1, fixture.Client.CreatedCount);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Deleted")]
    public async Task Terminal_failure_can_be_recovered_without_duplicate_active_jobs(string state)
    {
        var fixture = new QueueFixture();

        await fixture.Queue.EnqueueAsync("user@example.test", "User");
        fixture.Store.SetState("user@example.test", state);

        await fixture.Queue.EnqueueAsync("user@example.test", "User");

        Assert.Equal(2, fixture.Client.CreatedCount);
    }

    [Fact]
    public void Welcome_email_job_has_hangfire_retry_policy()
    {
        var method = typeof(WelcomeEmailJob).GetMethod(nameof(WelcomeEmailJob.SendAsync));

        var retry = Assert.Single(method!.GetCustomAttributes(inherit: true)
            .OfType<AutomaticRetryAttribute>());
        Assert.Equal(3, retry.Attempts);
    }

    private sealed class QueueFixture
    {
        public MemoryDeduplicationStore Store { get; } = new();
        public RecordingBackgroundJobClient Client { get; }
        public HangfireWelcomeEmailQueue Queue { get; }

        public QueueFixture()
        {
            Client = new RecordingBackgroundJobClient(Store);
            Queue = new HangfireWelcomeEmailQueue(Client, Store);
        }
    }

    private sealed class RecordingBackgroundJobClient(MemoryDeduplicationStore store)
        : IBackgroundJobClient
    {
        private int _createdCount;
        public int CreatedCount => _createdCount;

        public string Create(Job job, IState state)
        {
            var jobId = Interlocked.Increment(ref _createdCount).ToString();
            store.Track(job.Args[0].ToString()!, jobId, state.Name);
            return jobId;
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => false;
    }

    private sealed class MemoryDeduplicationStore : IWelcomeEmailDeduplicationStore
    {
        private readonly Dictionary<string, Entry> _entries = [];
        private readonly Dictionary<string, object> _locks = [];
        private readonly object _gate = new();

        public IWelcomeEmailReservation Acquire(string email)
        {
            var key = WelcomeEmailJobAssociation.Key(email);
            object emailLock;
            lock (_gate)
            {
                if (!_locks.TryGetValue(key, out emailLock!))
                    _locks[key] = emailLock = new();
            }

            Monitor.Enter(emailLock);
            return new Reservation(this, key, emailLock);
        }

        public void Track(string email, string jobId, string state)
        {
            lock (_gate)
                _entries[WelcomeEmailJobAssociation.Key(email)] = new(jobId, state);
        }

        public void SetState(string email, string? state)
        {
            lock (_gate)
            {
                var key = WelcomeEmailJobAssociation.Key(email);
                _entries[key] = _entries[key] with { State = state };
            }
        }

        private sealed class Reservation(
            MemoryDeduplicationStore owner,
            string key,
            object emailLock) : IWelcomeEmailReservation
        {
            public bool CanEnqueue()
            {
                lock (owner._gate)
                {
                    return !owner._entries.TryGetValue(key, out var entry) ||
                        WelcomeEmailDeduplication.CanEnqueue(entry.State);
                }
            }

            public void Dispose() => Monitor.Exit(emailLock);
        }

        private sealed record Entry(string JobId, string? State);
    }
}
