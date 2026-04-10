using System.Data;
using System.Data.Common;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Internal;

/// <summary>
/// Creates tables for tenant-level internal <see cref="DbContext"/> instances
/// (Templating, Timeline, BlobStorage, DataExchange, QueryEngine, Webhooks, Notifications).
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="ICurrentTenant.Change"/> to activate the tenant context before
/// creating the <see cref="DbContext"/>. After creation, explicitly activates the
/// tenant schema on the connection via <see cref="ITenantSchemaActivator"/> when
/// running in SchemaPerTenant mode.
/// </para>
/// <para>
/// Schema activation is necessary because module extensions register standard
/// <see cref="IDbContextFactory{TContext}"/> instances (via <c>AddGranitDbContext</c>
/// or <c>AddDbContextFactory</c>) that do not include
/// <see cref="TenantSchemaConnectionInterceptor"/>. Only
/// <c>TenantPerSchemaDbContextFactory</c> (registered by <c>AddGranitIsolatedDbContext</c>)
/// wires the interceptor automatically.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The tenant DbContext type whose tables should be created.</typeparam>
internal sealed partial class TenantInternalDbContextEnsurer<TContext>(
    IDbContextFactory<TContext> factory,
    ICurrentTenant currentTenant,
    IServiceProvider serviceProvider,
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

            await ActivateTenantSchemaAsync(db, tenantId, cancellationToken).ConfigureAwait(false);

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

        await ActivateTenantSchemaAsync(db, currentTenant.Id!.Value, cancellationToken).ConfigureAwait(false);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = db.Model
            .GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() is not null);

        if (entityType is null)
        {
            return true;
        }

        string tableName = entityType.GetTableName()!;

        // Query information_schema using the current search_path (set by ActivateTenantSchemaAsync).
        // This avoids Npgsql error-level log noise when the table does not exist yet.
        // The search_path resolves the schema, so we only filter by table_name.
        string sql = string.Concat(
            "SELECT COUNT(1) AS \"Value\" FROM information_schema.tables WHERE table_name = '", tableName, "'");
        int count = await db.Database
            .SqlQueryRaw<int>(sql) // NOSONAR — table name from EF model metadata, not user input
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        if (count == 0)
        {
            LogTableNotFoundForTenant(ContextName, tableName, currentTenant.Id);
        }

        return count > 0;
    }

    /// <summary>
    /// Activates the tenant schema on the DbContext connection when running in
    /// SchemaPerTenant mode. No-op when <see cref="ITenantSchemaProvider"/> or
    /// <see cref="ITenantSchemaActivator"/> are not registered (SharedDatabase
    /// or DatabasePerTenant modes).
    /// </summary>
    private async Task ActivateTenantSchemaAsync(
        TContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        ITenantSchemaProvider? schemaProvider = serviceProvider.GetService<ITenantSchemaProvider>();
        ITenantSchemaActivator? schemaActivator = serviceProvider.GetService<ITenantSchemaActivator>();

        if (schemaProvider is null || schemaActivator is null)
        {
            return;
        }

        string schemaName = await schemaProvider
            .GetSchemaNameAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        DbConnection connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await schemaActivator
            .ActivateSchemaAsync(connection, schemaName, cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Probe for '{ContextName}' table '{TableName}' not found for tenant {TenantId} — will create tables.")]
    private partial void LogTableNotFoundForTenant(string contextName, string tableName, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating tables for tenant internal DbContext '{ContextName}' (tenant {TenantId}).")]
    private partial void LogCreatingTablesForTenant(string contextName, Guid tenantId);
}
