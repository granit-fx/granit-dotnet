using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookDeliveryAttempt"/>.
/// Mirrors the dispatch contract of <see cref="EfWebhookSubscriptionQueryableSource"/>.
/// </summary>
internal sealed class EfWebhookDeliveryAttemptQueryableSource : IQueryableSource<WebhookDeliveryAttempt>, IDisposable
{
    private readonly DualScopeStorageMode _storageMode;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<WebhooksHostDbContext> _hostFactory;
    private readonly IDbContextFactory<WebhooksTenantDbContext>? _tenantFactory;
    private DbContext? _context;

    public EfWebhookDeliveryAttemptQueryableSource(
        WebhooksEntityFrameworkCoreOptions options,
        ICurrentTenant currentTenant,
        IDbContextFactory<WebhooksHostDbContext> hostFactory,
        IDbContextFactory<WebhooksTenantDbContext>? tenantFactory = null)
    {
        _storageMode = options.StorageMode;
        _bypassTenantFilter = !currentTenant.IsAvailable;
        _hostFactory = hostFactory;
        _tenantFactory = tenantFactory;
    }

    public IQueryable<WebhookDeliveryAttempt> GetQueryable()
    {
        if (_storageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            throw new NotSupportedException(
                "Cross-tenant host-admin browse of webhook delivery attempts under DualScopeStorageMode.Segregated " +
                "requires UNION ALL across host and every tenant schema — deferred to Phase 2C of Epic #2377. " +
                "Use DualScopeStorageMode.Shared if cross-tenant admin browsing is required today.");
        }

        _context ??= OpenContext();

        IQueryable<WebhookDeliveryAttempt> query = ((IWebhooksDbContext)_context)
            .WebhookDeliveryAttempts.AsNoTracking();

        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public void Dispose() => _context?.Dispose();

    private DbContext OpenContext() => _storageMode switch
    {
        DualScopeStorageMode.Shared => _hostFactory.CreateDbContext(),
        DualScopeStorageMode.Segregated => _tenantFactory!.CreateDbContext(),
        _ => throw new InvalidOperationException($"Unknown DualScopeStorageMode: {_storageMode}."),
    };
}
