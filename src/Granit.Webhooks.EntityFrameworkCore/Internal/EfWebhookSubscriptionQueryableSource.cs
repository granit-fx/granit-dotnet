using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookSubscription"/>.
/// Provides the queryable backbone for browsing endpoints exposed via <c>Granit.QueryEngine</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared mode.</b> Opens the host context (which under <c>Shared</c> holds every
/// subscription). When no tenant context is active (host admin), the
/// <see cref="GranitFilterNames.MultiTenant"/> filter is bypassed so all subscriptions are
/// returned cross-tenant.
/// </para>
/// <para>
/// <b>Segregated mode.</b> When a tenant context is active, opens the tenant-isolated
/// context. Cross-tenant host-admin browsing requires a <c>UNION ALL</c> across the host
/// context and every tenant schema — deferred to a follow-up PR (Phase 2C of #2377);
/// <see cref="GetQueryable"/> throws <see cref="NotSupportedException"/> on that path.
/// </para>
/// </remarks>
internal sealed class EfWebhookSubscriptionQueryableSource : IQueryableSource<WebhookSubscription>, IDisposable
{
    private readonly DualScopeStorageMode _storageMode;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<WebhooksHostDbContext> _hostFactory;
    private readonly IDbContextFactory<WebhooksTenantDbContext>? _tenantFactory;
    private DbContext? _context;

    public EfWebhookSubscriptionQueryableSource(
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

    public IQueryable<WebhookSubscription> GetQueryable()
    {
        if (_storageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            throw new NotSupportedException(
                "Cross-tenant host-admin browse of webhook subscriptions under DualScopeStorageMode.Segregated " +
                "requires UNION ALL across host and every tenant schema — deferred to Phase 2C of Epic #2377. " +
                "Use DualScopeStorageMode.Shared if cross-tenant admin browsing is required today.");
        }

        _context ??= OpenContext();

        IQueryable<WebhookSubscription> query = ((IWebhooksDbContext)_context)
            .WebhookSubscriptions.AsNoTracking();

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
