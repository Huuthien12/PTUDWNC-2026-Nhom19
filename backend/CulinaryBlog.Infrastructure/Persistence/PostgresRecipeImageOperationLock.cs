using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Buffers.Binary;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class PostgresRecipeImageOperationLock(AppDbContext db) : IRecipeImageOperationLock
{
    private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

    public async Task<IAsyncDisposable> AcquireAsync(
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        if (recipeId == Guid.Empty) throw new ArgumentException("Recipe ID cannot be empty.", nameof(recipeId));

        var connection = db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection)
            throw new InvalidOperationException("Recipe image operation locks require PostgreSQL.");

        var lockKey = CreateLockKey(recipeId);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var acquired = false;
        try
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < AcquisitionTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await TryAcquireAsync(connection, lockKey, cancellationToken))
                {
                    acquired = true;
                    return new Lease(db, connection, lockKey);
                }

                await Task.Delay(RetryInterval, cancellationToken);
            }

            throw new ServiceUnavailableException(
                "RECIPE_IMAGE_LOCK_TIMEOUT",
                "Image operation is temporarily busy. Retry the request.");
        }
        catch
        {
            if (acquired)
            {
                try { await UnlockAsync(connection, lockKey); }
                catch { NpgsqlConnection.ClearPool((NpgsqlConnection)connection); }
            }
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    private static long CreateLockKey(Guid recipeId)
    {
        var hash = SHA256.HashData(recipeId.ToByteArray());
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    private static async Task<bool> TryAcquireAsync(
        System.Data.Common.DbConnection connection,
        long lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = CreateLockCommand(connection, "SELECT pg_try_advisory_lock(@lock_key);", lockKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("PostgreSQL did not return an advisory lock result."));
    }

    private static async Task<bool> UnlockAsync(System.Data.Common.DbConnection connection, long lockKey)
    {
        await using var command = CreateLockCommand(connection, "SELECT pg_advisory_unlock(@lock_key);", lockKey);
        command.CommandTimeout = 5;
        return (bool)(await command.ExecuteScalarAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("PostgreSQL did not return an advisory unlock result."));
    }

    private static System.Data.Common.DbCommand CreateLockCommand(
        System.Data.Common.DbConnection connection,
        string sql,
        long lockKey)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 5;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lock_key";
        parameter.DbType = DbType.Int64;
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);
        return command;
    }

    private sealed class Lease(
        AppDbContext db,
        System.Data.Common.DbConnection connection,
        long lockKey) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try
            {
                if (connection.State != ConnectionState.Open || !await UnlockAsync(connection, lockKey))
                    throw new InvalidOperationException("The PostgreSQL recipe image lock could not be confirmed released.");
            }
            catch
            {
                if (connection is NpgsqlConnection postgresConnection)
                    NpgsqlConnection.ClearPool(postgresConnection);
                throw;
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
