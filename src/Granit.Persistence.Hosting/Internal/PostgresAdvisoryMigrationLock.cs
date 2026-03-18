using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Granit.Persistence.Hosting.Internal;

/// <summary>
/// Distributed migration lock using PostgreSQL advisory locks.
/// </summary>
/// <remarks>
/// <para>
/// Uses a raw <see cref="NpgsqlConnection"/> (NOT EF Core) because advisory locks are
/// session-scoped — they are tied to the physical connection. EF Core's connection pooling
/// would release the connection (and the lock) after each command.
/// </para>
/// <para>
/// The connection is kept open for the entire migration duration and closed when
/// the returned <see cref="IAsyncDisposable"/> is disposed.
/// </para>
/// </remarks>
internal sealed partial class PostgresAdvisoryMigrationLock(
    IConfiguration configuration,
    ILogger<PostgresAdvisoryMigrationLock> logger) : IGranitMigrationLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            LogNoConnectionString();
            return NoOpHandle.Instance;
        }

        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // hashtext returns a 32-bit integer hash — unique enough for advisory locks
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT pg_try_advisory_lock(hashtext('{resource}'))";

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        bool acquired = result is true;

        if (!acquired)
        {
            LogLockNotAcquired(resource);
            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        LogLockAcquired(resource);
        return new AdvisoryLockHandle(connection, resource, logger);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No DefaultConnection configured. Skipping distributed migration lock.")]
    private partial void LogNoConnectionString();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not acquire migration lock for '{Resource}'. Another instance is migrating.")]
    private partial void LogLockNotAcquired(string resource);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Acquired migration lock for '{Resource}'.")]
    private partial void LogLockAcquired(string resource);

    private sealed partial class AdvisoryLockHandle(
        NpgsqlConnection connection,
        string resource,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using NpgsqlCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT pg_advisory_unlock(hashtext('{resource}'))";
                await command.ExecuteScalarAsync().ConfigureAwait(false);
                LogLockReleased(resource);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Released migration lock for '{Resource}'.")]
        private partial void LogLockReleased(string resource);
    }

    private sealed class NoOpHandle : IAsyncDisposable
    {
        public static readonly NoOpHandle Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
