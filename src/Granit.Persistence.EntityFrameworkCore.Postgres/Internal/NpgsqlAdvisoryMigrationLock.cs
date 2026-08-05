using System.Buffers.Binary;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
/// The lock key is a <b>stable bigint</b>: the first 8 bytes (big-endian) of the SHA-256 of
/// the resource name — contractual across PostgreSQL versions and process restarts, unlike
/// the previous <c>hashtext()</c> key (internal PG function, int32, collision-prone).
/// </para>
/// <para>
/// <b>Rolling-upgrade transition (epic #3143 Phase 6)</b>: this version acquires BOTH the
/// new bigint key and the legacy <c>hashtext()</c> key, new key first. An old-version
/// replica holds only the legacy key, so a mixed fleet can never end up with two "lock
/// holders": the new replica wins the bigint key but then fails the legacy acquire and
/// backs off (releasing the bigint key). The legacy acquire is dropped in the next minor —
/// see CHANGELOG for the sequence.
/// </para>
/// <para>
/// When lock prerequisites are missing (connection string, provider factory) the behavior
/// is governed by <see cref="GranitMigrateOptions.RequireDistributedLock"/>: outside
/// Development the lock <b>fails closed</b> (throws — the migration aborts non-zero)
/// instead of degrading to an unlocked no-op run.
/// </para>
/// <para>
/// The connection is kept open for the entire migration duration and disposed when
/// the returned <see cref="IAsyncDisposable"/> is disposed. Registered automatically by
/// <c>AddGranitPostgres()</c>.
/// </para>
/// </remarks>
internal sealed partial class NpgsqlAdvisoryMigrationLock(
    IConfiguration configuration,
    IOptions<GranitMigrateOptions> options,
    ILogger<NpgsqlAdvisoryMigrationLock> logger,
    IHostEnvironment? environment = null) : IGranitMigrationLock
{
    /// <summary>
    /// Stable advisory key: first 8 bytes (big-endian) of SHA-256 of the resource name.
    /// </summary>
    internal static long ComputeAdvisoryKey(string resource) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resource, CancellationToken cancellationToken)
    {
        bool required = options.Value.IsDistributedLockRequired(environment);
        string connectionStringName = options.Value.ConnectionStringName;
        string? connectionString = configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrEmpty(connectionString))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    $"Distributed migration lock requires the '{connectionStringName}' connection "
                    + "string, which is not configured. Refusing to migrate unlocked. Configure the "
                    + "connection string (or 'Persistence:Migrate:ConnectionStringName'), or set "
                    + "'Persistence:Migrate:RequireDistributedLock' to false for single-instance hosts.");
            }

            LogNoConnectionString(connectionStringName);
            return NoOpHandle.Instance;
        }

        if (!DbProviderFactories.TryGetFactory("Npgsql", out DbProviderFactory? factory))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    "Distributed migration lock requires the Npgsql DbProviderFactory, which is not "
                    + "registered. Refusing to migrate unlocked. Call AddGranitPostgres(), or set "
                    + "'Persistence:Migrate:RequireDistributedLock' to false for single-instance hosts.");
            }

            LogNoProviderFactory();
            return NoOpHandle.Instance;
        }

        long key = ComputeAdvisoryKey(resource);

        DbConnection connection = factory.CreateConnection()!;
        // Advisory locks are session-scoped, and POOLED connections outlive DisposeAsync
        // (the session — and every advisory lock it holds — returns to the pool alive).
        // Pooling=false makes dispose a real disconnect, so every failure path below
        // releases by construction, and no reused session can carry phantom locks.
        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        connectionStringBuilder["Pooling"] = "false";
        connection.ConnectionString = connectionStringBuilder.ConnectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // New stable key first: a mixed fleet's old replicas contend only on the legacy
            // key, so winning the bigint key alone is not enough to proceed.
            if (!await TryAdvisoryLockAsync(connection, key, cancellationToken).ConfigureAwait(false))
            {
                LogLockNotAcquired(resource);
                await connection.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            if (!await TryLegacyAdvisoryLockAsync(connection, resource, cancellationToken).ConfigureAwait(false))
            {
                // An old-version replica holds the legacy key — back off completely so it
                // remains the single holder. Explicit unlock (belt) + unpooled dispose
                // (braces) both release the bigint key.
                await TryUnlockAsync(connection, key).ConfigureAwait(false);
                LogLockNotAcquired(resource);
                await connection.DisposeAsync().ConfigureAwait(false);
                return null;
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        LogLockAcquired(resource, key);
        return new AdvisoryLockHandle(connection, resource, key, logger);
    }

    private static async Task<bool> TryAdvisoryLockAsync(
        DbConnection connection, long key, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        DbParameter param = command.CreateParameter();
        param.ParameterName = "key";
        param.Value = key;
        command.Parameters.Add(param);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is true;
    }

    private static async Task TryUnlockAsync(DbConnection connection, long key)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@key)";
        DbParameter param = command.CreateParameter();
        param.ParameterName = "key";
        param.Value = key;
        command.Parameters.Add(param);
        await command.ExecuteScalarAsync().ConfigureAwait(false);
    }

    private static async Task<bool> TryLegacyAdvisoryLockAsync(
        DbConnection connection, string resource, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@resource))";
        DbParameter param = command.CreateParameter();
        param.ParameterName = "resource";
        param.Value = resource;
        command.Parameters.Add(param);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is true;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No '{ConnectionStringName}' connection string configured. Skipping distributed migration lock.")]
    private partial void LogNoConnectionString(string connectionStringName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Npgsql DbProviderFactory not registered. Skipping distributed migration lock.")]
    private partial void LogNoProviderFactory();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not acquire migration lock for '{Resource}'. Another instance is migrating.")]
    private partial void LogLockNotAcquired(string resource);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Acquired migration lock for '{Resource}' (advisory key {Key}).")]
    private partial void LogLockAcquired(string resource, long key);

    private sealed partial class AdvisoryLockHandle(
        DbConnection connection,
        string resource,
        long key,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using DbCommand command = connection.CreateCommand();
                // Release both keys of the transition dual-acquire (see class remarks).
                command.CommandText =
                    "SELECT pg_advisory_unlock(@key), pg_advisory_unlock(hashtext(@resource))";
                DbParameter keyParam = command.CreateParameter();
                keyParam.ParameterName = "key";
                keyParam.Value = key;
                command.Parameters.Add(keyParam);
                DbParameter resourceParam = command.CreateParameter();
                resourceParam.ParameterName = "resource";
                resourceParam.Value = resource;
                command.Parameters.Add(resourceParam);
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
    /// provider not registered) and <see cref="GranitMigrateOptions.RequireDistributedLock"/>
    /// resolves to <c>false</c>. Migrations proceed without a distributed lock.
    /// </summary>
    private sealed class NoOpHandle : IAsyncDisposable
    {
        public static readonly NoOpHandle Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
