using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Internal;

/// <summary>
/// Creates tables for host-level internal <see cref="DbContext"/> instances
/// (BackgroundJobs, BFF, MultiTenancy, Features, OpenIddict, Auditing, Localization).
/// </summary>
/// <remarks>
/// Uses two separate <see cref="DbContext"/> instances to avoid PostgreSQL connection-state
/// contamination: one for probing table existence (which may throw) and a fresh one for
/// <see cref="IRelationalDatabaseCreator.CreateTablesAsync"/>.
/// </remarks>
/// <typeparam name="TContext">The host DbContext type whose tables should be created.</typeparam>
internal sealed partial class HostInternalDbContextEnsurer<TContext>(
    IDbContextFactory<TContext> factory,
    ILogger<HostInternalDbContextEnsurer<TContext>> logger) : IHostInternalDbContextEnsurer
    where TContext : DbContext
{
    /// <inheritdoc/>
    public string ContextName => typeof(TContext).Name;

    /// <inheritdoc/>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (await ProbeTableExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await using TContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

        LogCreatingTables(ContextName);
        await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ProbeTableExistsAsync(CancellationToken cancellationToken)
    {
        await using TContext db = await factory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = db.Model
            .GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() is not null);

        if (entityType is null)
        {
            return true;
        }

        string tableName = entityType.GetTableName()!;
        // Host internal tables should be in the host schema. If the model has no
        // schema (tenant-internal modules like Templating/Notifications), fall back
        // to HostDbSchema so the probe looks in the correct schema.
        string? schema = entityType.GetSchema() ?? GranitDbDefaults.HostDbSchema;

        // Query information_schema instead of probing with SELECT — avoids
        // Npgsql error-level log noise when the table does not exist yet.
        string sql = schema is not null
            ? string.Concat(
                "SELECT COUNT(1) AS \"Value\" FROM information_schema.tables WHERE table_schema = '", schema,
                "' AND table_name = '", tableName, "'")
            : string.Concat(
                "SELECT COUNT(1) AS \"Value\" FROM information_schema.tables WHERE table_name = '", tableName, "'");

        int count = await db.Database
            .SqlQueryRaw<int>(sql) // NOSONAR — schema/table from EF model metadata, not user input
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        if (count == 0)
        {
            string qualifiedName = schema is not null ? $"{schema}.{tableName}" : tableName;
            LogTableNotFound(ContextName, qualifiedName);
        }

        return count > 0;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Probe for '{ContextName}' table '{TableName}' returned not found — will create tables.")]
    private partial void LogTableNotFound(string contextName, string tableName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating tables for host internal DbContext '{ContextName}'.")]
    private partial void LogCreatingTables(string contextName);
}
