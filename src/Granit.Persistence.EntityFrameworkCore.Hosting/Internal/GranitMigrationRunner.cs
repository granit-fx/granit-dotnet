using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Internal;

/// <summary>
/// Orchestrates every migration path — full EF Core schema migrations across all
/// <see cref="IMigratableModule{TContext}"/> modules discovered from the Granit module
/// dependency graph (<see cref="MigrationRunMode.Full"/>), and the startup resumption of
/// pending data-migration cycles (<see cref="MigrationRunMode.ResumeBatches"/>) — under
/// one distributed lock, one timeout, and one exit-code contract.
/// </summary>
internal sealed partial class GranitMigrationRunner(
    GranitApplication application,
    IServiceScopeFactory scopeFactory,
    IGranitMigrationLock migrationLock,
    IOptions<GranitMigrateOptions> options,
    ILogger<GranitMigrationRunner> logger,
    IMigrationBatchResumer? batchResumer = null,
    IHostEnvironment? environment = null) : IGranitMigrationRunner
{
    private readonly GranitMigrateOptions _options = options.Value;

    public async Task<int> RunAsync(
        MigrationRunMode mode = MigrationRunMode.Full,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);
        CancellationToken ct = timeoutCts.Token;

        List<(GranitModule Module, Type DbContextType)> migratableModules = [];

        if (mode == MigrationRunMode.Full)
        {
            // Discover migratable modules in topological order
            migratableModules = DiscoverMigratableModules();

            if (migratableModules.Count == 0)
            {
                LogNoMigratableModules();
                return 0;
            }

            LogMigrationStart(migratableModules.Count);
        }

        // Fail closed (epic #3143 Phase 6): a host without a real distributed lock must
        // refuse to migrate instead of migrating unlocked and exiting 0. NullMigrationLock
        // is the "no provider registered" fallback — with N replicas it would let every
        // one of them migrate concurrently.
        if (migrationLock is NullMigrationLock && _options.IsDistributedLockRequired(environment))
        {
            LogNoDistributedLock();
            return 1;
        }

        // One distributed lock for every mode: with N replicas, exactly one migrates or
        // resumes — the others skip. A per-mode lock resource would let a full run and a
        // startup resume overlap, dispatching batch commands mid-schema-migration.
        await using IAsyncDisposable? lockHandle = await migrationLock
            .TryAcquireAsync("GranitMigration", ct)
            .ConfigureAwait(false);

        if (lockHandle is null)
        {
            LogMigrationSkipped();
            return 0;
        }

        try
        {
            if (mode == MigrationRunMode.ResumeBatches)
            {
                return await ResumeBatchesAsync(ct).ConfigureAwait(false);
            }

            // Force PostConfigure<TenantIsolationOptions> to run, setting
            // GranitDbDefaults.HostDbSchema before any EF Core model compilation.
            await using (AsyncServiceScope initScope = scopeFactory.CreateAsyncScope())
            {
                _ = initScope.ServiceProvider.GetService<IOptions<TenantIsolationOptions>>()?.Value;

                // Create the host schema if configured (EF Core never creates schemas).
                if (GranitDbDefaults.HostDbSchema is not null)
                {
                    IConfiguration? config = initScope.ServiceProvider.GetService<IConfiguration>();
                    string? connectionString = config?.GetConnectionString(_options.ConnectionStringName);
                    if (connectionString is not null)
                    {
                        await SchemaEnsurer.EnsureSchemasAsync(
                            connectionString, cancellationToken: ct,
                            schemas: GranitDbDefaults.HostDbSchema).ConfigureAwait(false);
                    }
                }
            }

            // Migrate each DbContext in topological order
            foreach ((GranitModule module, Type dbContextType) in migratableModules)
            {
                await MigrateWithRetryAsync(module, dbContextType, ct).ConfigureAwait(false);
            }

            // Migrate external (non-EF Core) stores (e.g., Wolverine message storage)
            await MigrateExternalStoresAsync(ct).ConfigureAwait(false);

            // Resolve tenant enumerator for post-seed passes

            // Data seeding — host/tenant split:
            // 1. Host pass: IHostDataSeedContributor + legacy (IsHostOnly=true)
            //    → creates tenants, roles, OpenIddict apps, etc.
            // 2. Re-migrate per-tenant: seeding may have created tenants (cold start).
            // 3. Ensure tenant internal tables.
            // 4. Tenant pass: legacy (IsHostOnly=false) + ITenantDataSeedContributor per tenant
            //    → seeds tenant-specific data (products, articles, users).
            if (_options.SeedAfterMigration)
            {
                await SeedHostAsync(ct).ConfigureAwait(false);

                // Post-seed per-tenant migration: seeding may have created tenants
                // (cold start). Re-run migrations — ITenantEnumerator now finds them.
                foreach ((GranitModule module, Type dbContextType) in migratableModules)
                {
                    await MigrateDbContextAsync(module.GetType().Name, dbContextType, ct)
                        .ConfigureAwait(false);
                }

                await SeedTenantsAsync(ct).ConfigureAwait(false);
            }

            LogMigrationCompleted();
            return 0;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogMigrationTimeout(_options.Timeout);
            return 1;
        }
        catch (OperationCanceledException)
        {
            // External cancellation — rethrow to honor the caller's CancellationToken
            throw;
        }
        catch (Exception ex)
        {
            LogMigrationFailed(ex);
            return 1;
        }
    }

    private List<(GranitModule Module, Type DbContextType)> DiscoverMigratableModules()
    {
        List<(GranitModule, Type)> result = [];

        foreach (GranitModule module in application.GetModuleInstances())
        {
            // A module may implement IMigratableModule<T> for multiple DbContexts
            // (e.g., a host DbContext + a tenant DbContext). Scan all implemented
            // generic interfaces to discover every DbContext type.
            bool found = false;
            foreach (Type iface in module.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IMigratableModule<>))
                {
                    Type dbContextType = iface.GetGenericArguments()[0];
                    LogModuleDiscovered(module.GetType().Name, dbContextType.Name);
                    result.Add((module, dbContextType));
                    found = true;
                }
            }

            // Fallback: non-generic IMigratableModule (custom DbContextType override)
            if (!found && module is IMigratableModule migratable)
            {
                Type dbContextType = migratable.DbContextType;
                LogModuleDiscovered(module.GetType().Name, dbContextType.Name);
                result.Add((module, dbContextType));
            }
        }

        return result;
    }

    /// <summary>
    /// Dispatches resume commands for pending data-migration cycles via
    /// <see cref="IMigrationBatchResumer"/>. Runs inside the shared lock/timeout envelope.
    /// </summary>
    private async Task<int> ResumeBatchesAsync(CancellationToken ct)
    {
        if (batchResumer is null)
        {
            // AddGranitPersistenceMigrations() was not called — there is no data-migration
            // infrastructure and therefore nothing to resume.
            LogNoResumeInfrastructure();
            return 0;
        }

        await batchResumer.ResumeAsync(ct).ConfigureAwait(false);
        return 0;
    }

    private async Task MigrateWithRetryAsync(GranitModule module, Type dbContextType, CancellationToken ct)
    {
        string moduleName = module.GetType().Name;

        for (int attempt = 1; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                await MigrateDbContextAsync(moduleName, dbContextType, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxRetries && !ct.IsCancellationRequested)
            {
                LogRetry(moduleName, attempt, _options.MaxRetries, ex);
                await Task.Delay(_options.RetryDelay, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task MigrateDbContextAsync(string moduleName, Type dbContextType, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Check if this DbContext is tenant-isolated (registered via AddGranitIsolatedDbContext)
        bool isIsolated = scope.ServiceProvider.GetServices<MultiTenancy.IsolatedDbContextMarker>()
            .Any(m => m.DbContextType == dbContextType);

        if (isIsolated)
        {
            ITenantEnumerator? tenantEnumerator = scope.ServiceProvider.GetService<ITenantEnumerator>();
            bool hasTenants = tenantEnumerator is not null
                && await HasTenantsAsync(tenantEnumerator, ct).ConfigureAwait(false);

            if (hasTenants)
            {
                await MigratePerTenantAsync(moduleName, dbContextType, scope.ServiceProvider, tenantEnumerator!, ct)
                    .ConfigureAwait(false);
            }

            // No tenants yet (cold start): skip — tables will be created per-tenant
            // by the post-seed re-migration pass once host seeders create tenants.
            return;
        }

        // SharedDatabase or single-tenant — migrate normally in default schema.
        LogMigratingContext(moduleName, dbContextType.Name);
        await using DbContext dbContext = await ResolveDbContextAsync(scope.ServiceProvider, dbContextType)
            .ConfigureAwait(false);
        await dbContext.Database.MigrateAsync(ct).ConfigureAwait(false);
        LogMigratedContext(moduleName, dbContextType.Name);
    }

    private async Task MigratePerTenantAsync(
        string moduleName,
        Type dbContextType,
        IServiceProvider serviceProvider,
        ITenantEnumerator tenantEnumerator,
        CancellationToken ct)
    {
        ITenantDbIsolator isolator = serviceProvider.GetRequiredService<ITenantDbIsolator>();
        ITenantSchemaProvider? schemaProvider = serviceProvider.GetService<ITenantSchemaProvider>();

        await foreach (Guid tenantId in tenantEnumerator.GetActiveTenantIdsAsync(ct).ConfigureAwait(false))
        {
            await using AsyncServiceScope tenantScope = scopeFactory.CreateAsyncScope();

            // Activate tenant context BEFORE resolving the DbContext so the scoped
            // TContext registration uses the IsolatedDbContextFactory (not the
            // SharedDatabase fallback which creates in public schema).
            ICurrentTenant currentTenant = tenantScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
            using IDisposable tenantChange = currentTenant.Change(tenantId);

            // Create tenant schema if SchemaPerTenant (PostgreSQL never auto-creates).
            if (schemaProvider is not null)
            {
                string schemaName = await schemaProvider.GetSchemaNameAsync(tenantId, ct).ConfigureAwait(false);

                IConfiguration? config = tenantScope.ServiceProvider.GetService<IConfiguration>();
                string? connectionString = config?.GetConnectionString(_options.ConnectionStringName);
                if (connectionString is not null)
                {
                    await SchemaEnsurer.EnsureSchemasAsync(
                        connectionString, cancellationToken: ct,
                        schemas: schemaName).ConfigureAwait(false);
                }
            }

            LogMigratingContextForTenant(moduleName, dbContextType.Name, tenantId);
            await using DbContext dbContext = await ResolveDbContextAsync(tenantScope.ServiceProvider, dbContextType)
                .ConfigureAwait(false);
            await isolator.IsolateAsync(dbContext, tenantId, ct).ConfigureAwait(false);
            await dbContext.Database.MigrateAsync(ct).ConfigureAwait(false);
            LogMigratedContextForTenant(moduleName, dbContextType.Name, tenantId);
        }
    }

    private async Task MigrateExternalStoresAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IEnumerable<IExternalStoreMigrator> migrators =
            scope.ServiceProvider.GetServices<IExternalStoreMigrator>();

        foreach (IExternalStoreMigrator migrator in migrators)
        {
            LogMigratingExternalStore(migrator.Name);
            await migrator.MigrateAsync(ct).ConfigureAwait(false);
            LogMigratedExternalStore(migrator.Name);
        }
    }

    private async Task SeedHostAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDataSeeder? seeder = scope.ServiceProvider.GetService<IDataSeeder>();

        if (seeder is null)
        {
            return;
        }

        LogSeedingStart();
        await seeder.SeedHostAsync(new DataSeedContext(), ct).ConfigureAwait(false);
        LogSeedingCompleted();
    }

    private async Task SeedTenantsAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDataSeeder? seeder = scope.ServiceProvider.GetService<IDataSeeder>();

        if (seeder is null)
        {
            return;
        }

        LogSeedingStart();
        await seeder.SeedTenantsAsync(new DataSeedContext(), ct).ConfigureAwait(false);
        LogSeedingCompleted();
    }

    /// <summary>
    /// Resolves a DbContext from a scoped service provider.
    /// </summary>
    /// <remarks>
    /// Always resolves the DbContext directly (not via IDbContextFactory).
    /// EF Core registers TContext as Scoped even when AddDbContextFactory is used,
    /// so scoped resolution works for both AddDbContext and AddDbContextFactory registrations.
    /// Using IDbContextFactory would fail because singleton factories capture the root
    /// IServiceProvider and cannot resolve scoped interceptors.
    /// </remarks>
    private static Task<DbContext> ResolveDbContextAsync(IServiceProvider serviceProvider, Type dbContextType) =>
        Task.FromResult((DbContext)serviceProvider.GetRequiredService(dbContextType));

    private static async Task<bool> HasTenantsAsync(ITenantEnumerator enumerator, CancellationToken ct)
    {
        IAsyncEnumerator<Guid> e = enumerator.GetActiveTenantIdsAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            return await e.MoveNextAsync().ConfigureAwait(false);
        }
        finally
        {
            await e.DisposeAsync().ConfigureAwait(false);
        }
    }

    // --- Log messages ---

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No modules implement IMigratableModule<T>. Nothing to migrate.")]
    private partial void LogNoMigratableModules();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting migrations for {Count} migratable module(s).")]
    private partial void LogMigrationStart(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Migration skipped — another instance holds the migration lock.")]
    private partial void LogMigrationSkipped();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "No data-migration infrastructure registered (AddGranitPersistenceMigrations). "
            + "Nothing to resume.")]
    private partial void LogNoResumeInfrastructure();

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "No distributed migration lock is registered (NullMigrationLock resolved) and "
            + "'Persistence:Migrate:RequireDistributedLock' resolves to true — refusing to run "
            + "unlocked. Register a provider lock (AddGranitPostgres / AddGranitSqlServer) or "
            + "set the option to false for single-instance hosts.")]
    private partial void LogNoDistributedLock();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Discovered migratable module '{ModuleName}' with DbContext '{ContextName}'.")]
    private partial void LogModuleDiscovered(string moduleName, string contextName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrating '{ModuleName}' ({ContextName})...")]
    private partial void LogMigratingContext(string moduleName, string contextName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrated '{ModuleName}' ({ContextName}) successfully.")]
    private partial void LogMigratedContext(string moduleName, string contextName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrating '{ModuleName}' ({ContextName}) for tenant '{TenantId}'...")]
    private partial void LogMigratingContextForTenant(string moduleName, string contextName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrated '{ModuleName}' ({ContextName}) for tenant '{TenantId}' successfully.")]
    private partial void LogMigratedContextForTenant(string moduleName, string contextName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Migration attempt {Attempt}/{MaxRetries} failed for '{ModuleName}'. Retrying...")]
    private partial void LogRetry(string moduleName, int attempt, int maxRetries, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrating external store '{StoreName}'...")]
    private partial void LogMigratingExternalStore(string storeName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrated external store '{StoreName}' successfully.")]
    private partial void LogMigratedExternalStore(string storeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Running data seeders...")]
    private partial void LogSeedingStart();

    [LoggerMessage(Level = LogLevel.Information, Message = "Data seeding completed.")]
    private partial void LogSeedingCompleted();

    [LoggerMessage(Level = LogLevel.Information, Message = "All migrations completed successfully.")]
    private partial void LogMigrationCompleted();

    [LoggerMessage(Level = LogLevel.Error, Message = "Migration timed out after {Timeout}.")]
    private partial void LogMigrationTimeout(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Migration failed.")]
    private partial void LogMigrationFailed(Exception ex);
}
