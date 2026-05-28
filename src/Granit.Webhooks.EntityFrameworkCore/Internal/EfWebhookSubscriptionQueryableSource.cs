using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Options;
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
/// <b>Segregated + tenant scope.</b> Opens the tenant-isolated context only.
/// </para>
/// <para>
/// <b>Segregated + host-admin scope.</b> Materialises across the host context plus every
/// tenant returned by <see cref="ITenantsAccessor"/> via
/// <see cref="ICurrentTenant.Change"/>, then exposes the result as an in-memory
/// <see cref="IQueryable{T}"/>. Filters, ordering and paging applied by
/// <c>Granit.QueryEngine</c> downstream run in LINQ-to-Objects, not translated to SQL —
/// acceptable for the admin browse use case (small subscription volumes per tenant).
/// A Postgres cross-schema materialised view is the documented optimisation path for
/// deployments with thousands of subscriptions per tenant.
/// </para>
/// <para>
/// Soft-dep on <see cref="ITenantsAccessor"/>: the default
/// <c>NullTenantsAccessor</c> returns an empty list when <c>Granit.MultiTenancy</c> is
/// not loaded, so single-tenant deployments still get a host-only result without a hard
/// package dependency.
/// </para>
/// </remarks>
internal sealed class EfWebhookSubscriptionQueryableSource : IQueryableSource<WebhookSubscription>, IDisposable
{
    private readonly DualScopeStorageMode _storageMode;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<WebhooksHostDbContext> _hostFactory;
    private readonly IDbContextFactory<WebhooksTenantDbContext>? _tenantFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantsAccessor _tenantsAccessor;
    private DbContext? _context;
    private List<WebhookSubscription>? _materialized;

    public EfWebhookSubscriptionQueryableSource(
        WebhooksEntityFrameworkCoreOptions options,
        ICurrentTenant currentTenant,
        ITenantsAccessor tenantsAccessor,
        IDbContextFactory<WebhooksHostDbContext> hostFactory,
        IDbContextFactory<WebhooksTenantDbContext>? tenantFactory = null)
    {
        _storageMode = options.StorageMode;
        _bypassTenantFilter = !currentTenant.IsAvailable;
        _hostFactory = hostFactory;
        _tenantFactory = tenantFactory;
        _currentTenant = currentTenant;
        _tenantsAccessor = tenantsAccessor;
    }

    public IQueryable<WebhookSubscription> GetQueryable()
    {
        if (_storageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            _materialized ??= MaterialiseAcrossAllTenants();
            return _materialized.AsQueryable();
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

    private List<WebhookSubscription> MaterialiseAcrossAllTenants()
    {
        List<WebhookSubscription> results = [];

        // Host context — all rows there are platform subscriptions.
        using (WebhooksHostDbContext host = _hostFactory.CreateDbContext())
        {
            results.AddRange(host.WebhookSubscriptions
                .AsNoTracking()
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .ToList());
        }

        if (_tenantFactory is null)
        {
            return results;
        }

        IReadOnlyList<(Guid Id, string Name)> tenants = _tenantsAccessor
            .GetAllAsync().GetAwaiter().GetResult();

        // Empty under NullTenantsAccessor — host-only result, no exception.
        foreach ((Guid id, string name) in tenants)
        {
            using (_currentTenant.Change(id, name))
            using (WebhooksTenantDbContext tenantCtx = _tenantFactory.CreateDbContext())
            {
                results.AddRange(tenantCtx.WebhookSubscriptions
                    .AsNoTracking()
                    .ToList());
            }
        }

        return results;
    }
}
