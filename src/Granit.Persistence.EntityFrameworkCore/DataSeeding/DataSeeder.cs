using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Default implementation of <see cref="IDataSeeder"/>.
/// Orchestrates host, tenant, and legacy seed contributors with resilient error handling.
/// </summary>
internal sealed partial class DataSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<DataSeeder> logger) : IDataSeeder
{
    /// <inheritdoc/>
    public async Task SeedHostAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Host contributors
        IEnumerable<IHostDataSeedContributor> hostContributors =
            scope.ServiceProvider.GetServices<IHostDataSeedContributor>();

        foreach (IHostDataSeedContributor contributor in hostContributors)
        {
            await ExecuteContributorAsync(
                contributor.GetType(), () => contributor.SeedAsync(context, cancellationToken))
                .ConfigureAwait(false);
        }

        // Legacy contributors — first pass with IsHostOnly=true
        await ExecuteLegacyContributorsAsync(scope.ServiceProvider, context, isHostOnly: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SeedTenantsAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        // Legacy contributors — second pass with IsHostOnly=false, no tenant context
        // (they manage their own ICurrentTenant.Change() calls internally)
        await ExecuteLegacyTenantPassAsync(context, cancellationToken).ConfigureAwait(false);

        // Tenant contributors — per-tenant with DataSeeder-managed context
        await using AsyncServiceScope providerScope = scopeFactory.CreateAsyncScope();
        IDataSeedTenantProvider? tenantProvider =
            providerScope.ServiceProvider.GetService<IDataSeedTenantProvider>();

        if (tenantProvider is null)
        {
            // Single-tenant: execute once without tenant context
            await SeedTenantContributorsAsync(new DataSeedContext(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await foreach (Guid tenantId in tenantProvider.GetTenantIdsAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            DataSeedContext tenantContext = new(tenantId);
            CopyProperties(context, tenantContext);

            await SeedTenantContributorsAsync(tenantContext, cancellationToken, tenantId)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        await SeedHostAsync(context, cancellationToken).ConfigureAwait(false);
        await SeedTenantsAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedTenantContributorsAsync(
        DataSeedContext context, CancellationToken cancellationToken, Guid? tenantId = null)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        IDisposable? tenantChange = null;
        if (tenantId.HasValue)
        {
            ICurrentTenant? currentTenant = scope.ServiceProvider.GetService<ICurrentTenant>();
            tenantChange = currentTenant?.Change(tenantId.Value);
        }

        try
        {
            IEnumerable<ITenantDataSeedContributor> contributors =
                scope.ServiceProvider.GetServices<ITenantDataSeedContributor>();

            foreach (ITenantDataSeedContributor contributor in contributors)
            {
                await ExecuteContributorAsync(
                    contributor.GetType(), () => contributor.SeedAsync(context, cancellationToken))
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            tenantChange?.Dispose();
        }
    }

    private async Task ExecuteLegacyContributorsAsync(
        IServiceProvider serviceProvider, DataSeedContext context, bool isHostOnly, CancellationToken cancellationToken)
    {
        IEnumerable<IDataSeedContributor> contributors =
            serviceProvider.GetServices<IDataSeedContributor>();

        DataSeedContext legacyContext = new(context.TenantId);
        legacyContext[DataSeedContext.HostOnlyKey] = isHostOnly;
        CopyProperties(context, legacyContext);

        foreach (IDataSeedContributor contributor in contributors)
        {
            await ExecuteContributorAsync(
                contributor.GetType(), () => contributor.SeedAsync(legacyContext, cancellationToken))
                .ConfigureAwait(false);
        }
    }

    private async Task ExecuteLegacyTenantPassAsync(DataSeedContext context, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await ExecuteLegacyContributorsAsync(scope.ServiceProvider, context, isHostOnly: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteContributorAsync(Type contributorType, Func<Task> action)
    {
        string contributorName = contributorType.FullName ?? contributorType.Name;

        try
        {
            LogContributorStarted(contributorName);
            await action().ConfigureAwait(false);
            LogContributorCompleted(contributorName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogContributorFailed(contributorName, ex);
        }
    }

    private static void CopyProperties(DataSeedContext source, DataSeedContext target)
    {
        foreach (KeyValuePair<string, object?> kvp in source.Properties)
        {
            if (kvp.Key != DataSeedContext.HostOnlyKey)
            {
                target.Properties[kvp.Key] = kvp.Value;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Executing data seed contributor '{ContributorName}'.")]
    private partial void LogContributorStarted(string contributorName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Data seed contributor '{ContributorName}' completed successfully.")]
    private partial void LogContributorCompleted(string contributorName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Data seed contributor '{ContributorName}' failed. Seeding continues with remaining contributors.")]
    private partial void LogContributorFailed(string contributorName, Exception ex);
}
