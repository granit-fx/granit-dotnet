using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Internal;

/// <summary>
/// Distributed migration lock using PostgreSQL advisory locks (<c>pg_try_advisory_lock</c>).
/// </summary>
/// <remarks>
/// <para>
/// Uses a raw <see cref="DbConnection"/> (NOT EF Core) because advisory locks are
/// session-scoped — they are tied to the physical connection. EF Core's connection pooling
/// would return the connection to the pool between commands, silently releasing the lock.
/// </para>
/// <para>
/// The connection is kept open for the entire migration duration and disposed when
/// the returned <see cref="IAsyncDisposable"/> is disposed.
/// </para>
/// <para>
/// Registered automatically by <c>AddGranitPostgres()</c>. The Npgsql provider factory
/// must be available at runtime (i.e., the host application references Npgsql).
/// </para>
/// </remarks>
internal sealed partial class NpgsqlAdvisoryMigrationLock(
    IConfiguration configuration,
    ILogger<NpgsqlAdvisoryMigrationLock> logger) : IGranitMigrationLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resource, CancellationToken cancellationToken)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            LogNoConnectionString();
            return NoOpHandle.Instance;
        }

        if (!DbProviderFactories.TryGetFactory("Npgsql", out DbProviderFactory? factory))
        {
            LogNoProviderFactory();
            return NoOpHandle.Instance;
        }

        DbConnection connection = factory.CreateConnection()!;
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@resource))";
        DbParameter param = command.CreateParameter();
        param.ParameterName = "resource";
        param.Value = resource;
        command.Parameters.Add(param);

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
        Message = "Npgsql DbProviderFactory not registered. Skipping distributed migration lock.")]
    private partial void LogNoProviderFactory();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not acquire migration lock for '{Resource}'. Another instance is migrating.")]
    private partial void LogLockNotAcquired(string resource);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Acquired migration lock for '{Resource}'.")]
    private partial void LogLockAcquired(string resource);

    private sealed partial class AdvisoryLockHandle(
        DbConnection connection,
        string resource,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using DbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(hashtext(@resource))";
                DbParameter param = command.CreateParameter();
                param.ParameterName = "resource";
                param.Value = resource;
                command.Parameters.Add(param);
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

    /// <summary>
    /// Returned when the lock infrastructure is unavailable (no connection string,
    /// provider not registered). Migrations proceed without a distributed lock.
    /// </summary>
    private sealed class NoOpHandle : IAsyncDisposable
    {
        public static readonly NoOpHandle Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
