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
        string? schema = entityType.GetSchema();
        ISqlGenerationHelper helper = db.GetService<ISqlGenerationHelper>();

        string qualifiedName = schema is not null
            ? $"{helper.DelimitIdentifier(schema)}.{helper.DelimitIdentifier(tableName)}"
            : helper.DelimitIdentifier(tableName);

        try
        {
            string probeSql = string.Concat("SELECT 1 FROM ", qualifiedName, " WHERE 1=0");
            await db.Database
                .ExecuteSqlRawAsync(probeSql, cancellationToken) // NOSONAR — table name from EF model, delimiter-escaped
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogTableNotFound(ContextName, qualifiedName);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Probe for '{ContextName}' table '{TableName}' returned not found — will create tables.")]
    private partial void LogTableNotFound(string contextName, string tableName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating tables for host internal DbContext '{ContextName}'.")]
    private partial void LogCreatingTables(string contextName);
}
