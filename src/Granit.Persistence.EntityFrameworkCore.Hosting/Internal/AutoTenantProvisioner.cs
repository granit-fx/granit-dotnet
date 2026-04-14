using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Internal;

/// <summary>
/// Default <see cref="ITenantProvisioner"/> that discovers all tenant-isolated DbContexts
/// via <see cref="IsolatedDbContextMarker"/> singletons, creates schemas, runs EF Core
/// migrations, and seeds tenant data.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the per-tenant migration logic of <see cref="GranitMigrationRunner.MigratePerTenantAsync"/>
/// but operates on a single newly created tenant at runtime rather than iterating all
/// existing tenants at cold start.
/// </para>
/// <para>
/// Each DbContext is resolved in its own scoped DI container with tenant context activated
/// via <see cref="ICurrentTenant.Change"/>. This ensures <see cref="IsolatedDbContextFactory{TContext}"/>
/// dispatches to the correct tenant-specific connection or schema.
/// </para>
/// <para>
/// All operations are idempotent: <c>CREATE SCHEMA IF NOT EXISTS</c>, EF Core migration
/// tracking, and seed contributors that guard against duplicate inserts. Wolverine's retry
/// policies can safely replay provisioning messages without side effects.
/// </para>
/// </remarks>
internal sealed partial class AutoTenantProvisioner(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoTenantProvisioner> logger) : ITenantProvisioner
{
    /// <inheritdoc/>
    public async Task ProvisionAsync(
        Guid tenantId,
        string tenantName,
        CancellationToken cancellationToken = default)
    {
        // Discover all isolated DbContext types registered via AddGranitIsolatedDbContext<T>().
        // IsolatedDbContextMarker is registered as singleton — safe to resolve from any scope.
        await using AsyncServiceScope discoveryScope = scopeFactory.CreateAsyncScope();
        var contextTypes = discoveryScope.ServiceProvider
            .GetServices<IsolatedDbContextMarker>()
            .Select(m => m.DbContextType)
            .ToList();

        if (contextTypes.Count == 0)
        {
            LogNoIsolatedContexts();
            return;
        }

        LogProvisioningStart(tenantId, tenantName, contextTypes.Count);

        // Resolve schema provider once — shared across all contexts.
        ITenantSchemaProvider? schemaProvider = discoveryScope.ServiceProvider
            .GetService<ITenantSchemaProvider>();

        // 1. For each isolated DbContext: ensure schema (if SchemaPerTenant) → isolate → migrate.
        // Schema creation uses the DbContext's own connection — no hardcoded connection string name.
        foreach (Type dbContextType in contextTypes)
        {
            await MigrateContextAsync(tenantId, tenantName, dbContextType, schemaProvider, cancellationToken)
                .ConfigureAwait(false);
        }

        // 2. Seed tenant-specific data (ITenantDataSeedContributor implementations).
        await SeedTenantDataAsync(tenantId, cancellationToken).ConfigureAwait(false);

        LogProvisioningComplete(tenantId);
    }

    private async Task MigrateContextAsync(
        Guid tenantId,
        string tenantName,
        Type dbContextType,
        ITenantSchemaProvider? schemaProvider,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope tenantScope = scopeFactory.CreateAsyncScope();
        IServiceProvider sp = tenantScope.ServiceProvider;

        // Activate tenant context BEFORE resolving the DbContext so the scoped
        // IsolatedDbContextFactory dispatches to the correct connection/schema.
        ICurrentTenant currentTenant = sp.GetRequiredService<ICurrentTenant>();
        using IDisposable tenantChange = currentTenant.Change(tenantId, tenantName);

        LogMigratingContext(dbContextType.Name, tenantId);

        // Direct scoped resolution (not IDbContextFactory) — same approach as
        // GranitMigrationRunner.ResolveDbContextAsync. Scoped resolution picks up
        // scoped interceptors that singleton factories cannot.
        var dbContext = (DbContext)sp.GetRequiredService(dbContextType);

        // Create tenant schema if SchemaPerTenant.
        // CRITICAL: use the connectionString overload — NOT the DbContext overload.
        // The DbContext overload opens the raw ADO.NET connection, which prevents
        // TenantSchemaConnectionInterceptor from firing on the subsequent MigrateAsync
        // (it only triggers on ConnectionOpened, and the connection is already open).
        // The connectionString overload creates a temporary connection, matching
        // GranitMigrationRunner.MigratePerTenantAsync behavior.
        if (schemaProvider is not null)
        {
            string schema = await schemaProvider.GetSchemaNameAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            string? connectionString = dbContext.Database.GetConnectionString();
            if (connectionString is not null)
            {
                await SchemaEnsurer.EnsureSchemasAsync(
                    connectionString, cancellationToken: cancellationToken,
                    schemas: schema).ConfigureAwait(false);
            }

            LogSchemaEnsured(tenantId, schema);
        }

        // Isolate the connection for this tenant (SET search_path for SchemaPerTenant,
        // no-op for SharedDatabase/DatabasePerTenant).
        ITenantDbIsolator isolator = sp.GetRequiredService<ITenantDbIsolator>();
        await isolator.IsolateAsync(dbContext, tenantId, cancellationToken).ConfigureAwait(false);

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        LogMigratedContext(dbContextType.Name, tenantId);
    }

    private async Task SeedTenantDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope seedScope = scopeFactory.CreateAsyncScope();
        IDataSeeder? seeder = seedScope.ServiceProvider.GetService<IDataSeeder>();

        if (seeder is null)
        {
            return;
        }

        LogSeedingTenant(tenantId);
        await seeder.SeedTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    // --- Log messages ---

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No tenant-isolated DbContexts registered. Tenant provisioning skipped.")]
    private partial void LogNoIsolatedContexts();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Provisioning tenant {TenantId} ({TenantName}) — {ContextCount} isolated DbContext(s) to migrate.")]
    private partial void LogProvisioningStart(Guid tenantId, string tenantName, int contextCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Schema '{Schema}' ensured for tenant {TenantId}.")]
    private partial void LogSchemaEnsured(Guid tenantId, string schema);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrating {ContextName} for tenant {TenantId}...")]
    private partial void LogMigratingContext(string contextName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrated {ContextName} for tenant {TenantId} successfully.")]
    private partial void LogMigratedContext(string contextName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Seeding data for tenant {TenantId}...")]
    private partial void LogSeedingTenant(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tenant {TenantId} provisioned successfully.")]
    private partial void LogProvisioningComplete(Guid tenantId);
}
