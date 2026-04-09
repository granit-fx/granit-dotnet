using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Internal;

/// <summary>
/// Creates tables for tenant-level internal <see cref="DbContext"/> instances
/// (Templating, Timeline, BlobStorage, DataExchange, QueryEngine, Webhooks, Notifications).
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="ICurrentTenant.Change"/> to activate the tenant context before
/// creating the <see cref="DbContext"/>. The factory dispatches to the correct
/// isolation strategy:
/// </para>
/// <list type="bullet">
///   <item><b>SchemaPerTenant</b>: <c>TenantPerSchemaDbContextFactory</c> adds
///   <c>SET search_path</c> interceptor → tables created in tenant schema.</item>
///   <item><b>DatabasePerTenant</b>: <c>TenantPerDatabaseDbContextFactory</c> resolves
///   tenant connection string → tables created in tenant database.</item>
///   <item><b>SharedDatabase</b>: <c>SharedDatabaseDbContextFactory</c> → tables created
///   once in default schema (same behavior as host ensurer).</item>
/// </list>
/// </remarks>
/// <typeparam name="TContext">The tenant DbContext type whose tables should be created.</typeparam>
internal sealed partial class TenantInternalDbContextEnsurer<TContext>(
    IDbContextFactory<TContext> factory,
    ICurrentTenant currentTenant,
    ILogger<TenantInternalDbContextEnsurer<TContext>> logger) : ITenantInternalDbContextEnsurer
    where TContext : DbContext
{
    /// <inheritdoc/>
    public string ContextName => typeof(TContext).Name;

    /// <inheritdoc/>
    public async Task EnsureCreatedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using (currentTenant.Change(tenantId))
        {
            if (await ProbeTableExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await using TContext db = await factory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            IRelationalDatabaseCreator creator = db.GetService<IRelationalDatabaseCreator>();

            LogCreatingTablesForTenant(ContextName, tenantId);
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
        }
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
        ISqlGenerationHelper helper = db.GetService<ISqlGenerationHelper>();

        // In SchemaPerTenant mode, search_path is already set by the interceptor.
        // Use unqualified table name — the search_path handles schema resolution.
        string qualifiedName = helper.DelimitIdentifier(tableName);

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
            LogTableNotFoundForTenant(ContextName, tableName, currentTenant.Id);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Probe for '{ContextName}' table '{TableName}' not found for tenant {TenantId} — will create tables.")]
    private partial void LogTableNotFoundForTenant(string contextName, string tableName, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating tables for tenant internal DbContext '{ContextName}' (tenant {TenantId}).")]
    private partial void LogCreatingTablesForTenant(string contextName, Guid tenantId);
}
