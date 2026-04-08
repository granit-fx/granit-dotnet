using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Internal;

/// <summary>
/// Orchestrates EF Core migrations across all <see cref="IMigratableModule{TContext}"/> modules
/// discovered from the Granit module dependency graph.
/// </summary>
internal sealed partial class GranitMigrationRunner(
    GranitApplication application,
    IServiceScopeFactory scopeFactory,
    IGranitMigrationLock migrationLock,
    GranitMigrateOptions options,
    ILogger<GranitMigrationRunner> logger) : IGranitMigrationRunner
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);
        CancellationToken ct = timeoutCts.Token;

        // Discover migratable modules in topological order
        List<(GranitModule Module, Type DbContextType)> migratableModules = DiscoverMigratableModules();

        if (migratableModules.Count == 0)
        {
            LogNoMigratableModules();
            return 0;
        }

        LogMigrationStart(migratableModules.Count);

        // Acquire distributed lock
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
            // Force PostConfigure<TenantIsolationOptions> to run, setting
            // GranitDbDefaults.HostDbSchema before any EF Core model compilation.
            await using (AsyncServiceScope initScope = scopeFactory.CreateAsyncScope())
            {
                _ = initScope.ServiceProvider.GetService<IOptions<TenantIsolationOptions>>()?.Value;

                // Create the host schema if configured (EF Core never creates schemas).
                if (GranitDbDefaults.HostDbSchema is not null)
                {
                    IConfiguration? config = initScope.ServiceProvider.GetService<IConfiguration>();
                    string? connectionString = config?.GetConnectionString("DefaultConnection");
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

            // Ensure Expand & Contract tracking table exists
            await EnsureExpandContractDbAsync(ct).ConfigureAwait(false);

            // Ensure internal DbContext tables (OpenIddict, etc.)
            await EnsureInternalDbContextsAsync(ct).ConfigureAwait(false);

            // Data seeding
            if (options.SeedAfterMigration)
            {
                await SeedAsync(ct).ConfigureAwait(false);
            }

            LogMigrationCompleted();
            return 0;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogMigrationTimeout(options.Timeout);
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
            if (module is IMigratableModule migratable)
            {
                Type dbContextType = migratable.DbContextType;
                LogModuleDiscovered(module.GetType().Name, dbContextType.Name);
                result.Add((module, dbContextType));
            }
        }

        return result;
    }

    private async Task MigrateWithRetryAsync(GranitModule module, Type dbContextType, CancellationToken ct)
    {
        string moduleName = module.GetType().Name;

        for (int attempt = 1; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                await MigrateDbContextAsync(moduleName, dbContextType, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < options.MaxRetries && !ct.IsCancellationRequested)
            {
                LogRetry(moduleName, attempt, options.MaxRetries, ex);
                await Task.Delay(options.RetryDelay, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task MigrateDbContextAsync(string moduleName, Type dbContextType, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Check for multi-tenant migration
        ITenantEnumerator? tenantEnumerator = scope.ServiceProvider.GetService<ITenantEnumerator>();

        if (tenantEnumerator is not null && await HasTenantsAsync(tenantEnumerator, ct).ConfigureAwait(false))
        {
            await MigratePerTenantAsync(moduleName, dbContextType, scope.ServiceProvider, tenantEnumerator, ct)
                .ConfigureAwait(false);
        }
        else
        {
            LogMigratingContext(moduleName, dbContextType.Name);
            await using DbContext dbContext = await ResolveDbContextAsync(scope.ServiceProvider, dbContextType)
                .ConfigureAwait(false);
            await dbContext.Database.MigrateAsync(ct).ConfigureAwait(false);
            LogMigratedContext(moduleName, dbContextType.Name);
        }
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

            // Create tenant schema if SchemaPerTenant (PostgreSQL never auto-creates).
            if (schemaProvider is not null)
            {
                string schemaName = await schemaProvider.GetSchemaNameAsync(tenantId, ct).ConfigureAwait(false);

                IConfiguration? config = tenantScope.ServiceProvider.GetService<IConfiguration>();
                string? connectionString = config?.GetConnectionString("DefaultConnection");
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

    private async Task EnsureExpandContractDbAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Resolve MigrationProgressDbContext dynamically to avoid exposing the internal type
        // in this assembly's public surface (which would cause ReflectionTypeLoadException
        // when other modules scan assemblies).
        IMigrationProgressDbEnsurer? ensurer = scope.ServiceProvider.GetService<IMigrationProgressDbEnsurer>();

        if (ensurer is null)
        {
            return;
        }

        LogCreatingExpandContractDb();
        await ensurer.EnsureCreatedAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureInternalDbContextsAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IEnumerable<IInternalDbContextEnsurer> ensurers =
            scope.ServiceProvider.GetServices<IInternalDbContextEnsurer>();

        foreach (IInternalDbContextEnsurer ensurer in ensurers)
        {
            LogEnsuringInternalContext(ensurer.ContextName);
            await ensurer.EnsureCreatedAsync(ct).ConfigureAwait(false);
            LogEnsuredInternalContext(ensurer.ContextName);
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDataSeeder? seeder = scope.ServiceProvider.GetService<IDataSeeder>();

        if (seeder is null)
        {
            return;
        }

        LogSeedingStart();
        DataSeedContext seedContext = new();
        await seeder.SeedAsync(seedContext, ct).ConfigureAwait(false);
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
        Message = "Creating Expand & Contract progress tracking table.")]
    private partial void LogCreatingExpandContractDb();

    [LoggerMessage(Level = LogLevel.Information, Message = "Running data seeders...")]
    private partial void LogSeedingStart();

    [LoggerMessage(Level = LogLevel.Information, Message = "Data seeding completed.")]
    private partial void LogSeedingCompleted();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Ensuring internal DbContext '{ContextName}' tables exist...")]
    private partial void LogEnsuringInternalContext(string contextName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Internal DbContext '{ContextName}' tables verified.")]
    private partial void LogEnsuredInternalContext(string contextName);

    [LoggerMessage(Level = LogLevel.Information, Message = "All migrations completed successfully.")]
    private partial void LogMigrationCompleted();

    [LoggerMessage(Level = LogLevel.Error, Message = "Migration timed out after {Timeout}.")]
    private partial void LogMigrationTimeout(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Migration failed.")]
    private partial void LogMigrationFailed(Exception ex);
}
