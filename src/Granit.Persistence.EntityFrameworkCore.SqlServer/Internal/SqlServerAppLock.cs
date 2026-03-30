using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;

/// <summary>
/// Distributed migration lock using SQL Server application locks (<c>sp_getapplock</c>).
/// </summary>
/// <remarks>
/// <para>
/// Uses <c>sp_getapplock</c> with <c>@LockOwner = 'Session'</c> so the lock is held at
/// the connection level and requires no open transaction. The connection is kept open for
/// the entire migration duration and disposed when the returned
/// <see cref="IAsyncDisposable"/> is disposed.
/// </para>
/// <para>
/// <c>@LockTimeout = 0</c> makes the call non-blocking: it returns immediately with a
/// negative value if the lock cannot be acquired (another instance is migrating).
/// </para>
/// <para>
/// Registered automatically by <c>AddGranitSqlServer()</c>. The Microsoft.Data.SqlClient
/// provider factory must be available at runtime.
/// </para>
/// </remarks>
internal sealed partial class SqlServerAppLock(
    IConfiguration configuration,
    ILogger<SqlServerAppLock> logger) : IGranitMigrationLock
{
    private const string ProviderInvariantName = "Microsoft.Data.SqlClient";

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resource, CancellationToken cancellationToken)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            LogNoConnectionString();
            return NoOpHandle.Instance;
        }

        if (!DbProviderFactories.TryGetFactory(ProviderInvariantName, out DbProviderFactory? factory))
        {
            LogNoProviderFactory();
            return NoOpHandle.Instance;
        }

        DbConnection connection = factory.CreateConnection()!;
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result INT;
            EXEC @result = sp_getapplock
                @Resource    = @resource,
                @LockMode    = 'Exclusive',
                @LockOwner   = 'Session',
                @LockTimeout = 0;
            SELECT @result;
            """;
        DbParameter param = command.CreateParameter();
        param.ParameterName = "resource";
        param.Value = resource;
        command.Parameters.Add(param);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        // sp_getapplock returns 0 or 1 for success, negative values for failure
        bool acquired = result is int returnCode && returnCode >= 0;

        if (!acquired)
        {
            LogLockNotAcquired(resource);
            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        LogLockAcquired(resource);
        return new AppLockHandle(connection, resource, logger);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No DefaultConnection configured. Skipping distributed migration lock.")]
    private partial void LogNoConnectionString();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Microsoft.Data.SqlClient DbProviderFactory not registered. Skipping distributed migration lock.")]
    private partial void LogNoProviderFactory();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not acquire migration lock for '{Resource}'. Another instance is migrating.")]
    private partial void LogLockNotAcquired(string resource);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Acquired migration lock for '{Resource}'.")]
    private partial void LogLockAcquired(string resource);

    private sealed partial class AppLockHandle(
        DbConnection connection,
        string resource,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using DbCommand command = connection.CreateCommand();
                command.CommandText = """
                    EXEC sp_releaseapplock
                        @Resource  = @resource,
                        @LockOwner = 'Session';
                    """;
                DbParameter param = command.CreateParameter();
                param.ParameterName = "resource";
                param.Value = resource;
                command.Parameters.Add(param);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
