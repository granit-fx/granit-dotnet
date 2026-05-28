using Granit.MultiTenancy;
using Granit.MultiTenancy.Stores;
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
/// <remarks>
/// Under <c>Segregated</c> + host-admin scope, delivery attempts are materialised across
/// host + every tenant schema. Delivery volumes grow faster than subscription counts
/// (every webhook delivery writes a row, ISO 27001 3-year retention), so admin dashboards
/// using this source should always paginate and filter on <c>OccurredAt</c>. A Postgres
/// cross-schema view is recommended for deployments with high delivery volume.
/// </remarks>
internal sealed class EfWebhookDeliveryAttemptQueryableSource : IQueryableSource<WebhookDeliveryAttempt>, IDisposable
{
    private readonly DualScopeStorageMode _storageMode;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<WebhooksHostDbContext> _hostFactory;
    private readonly IDbContextFactory<WebhooksTenantDbContext>? _tenantFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantReader? _tenantReader;
    private DbContext? _context;
    private List<WebhookDeliveryAttempt>? _materialized;

    public EfWebhookDeliveryAttemptQueryableSource(
        WebhooksEntityFrameworkCoreOptions options,
        ICurrentTenant currentTenant,
        IDbContextFactory<WebhooksHostDbContext> hostFactory,
        IDbContextFactory<WebhooksTenantDbContext>? tenantFactory = null,
        ITenantReader? tenantReader = null)
    {
        _storageMode = options.StorageMode;
        _bypassTenantFilter = !currentTenant.IsAvailable;
        _hostFactory = hostFactory;
        _tenantFactory = tenantFactory;
        _currentTenant = currentTenant;
        _tenantReader = tenantReader;
    }

    public IQueryable<WebhookDeliveryAttempt> GetQueryable()
    {
        if (_storageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            _materialized ??= MaterialiseAcrossAllTenants();
            return _materialized.AsQueryable();
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

    private List<WebhookDeliveryAttempt> MaterialiseAcrossAllTenants()
    {
        List<WebhookDeliveryAttempt> results = [];

        using (WebhooksHostDbContext host = _hostFactory.CreateDbContext())
        {
            results.AddRange(host.WebhookDeliveryAttempts
                .AsNoTracking()
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .ToList());
        }

        if (_tenantReader is null || _tenantFactory is null)
        {
            return results;
        }

        IReadOnlyList<TenantData> tenants = _tenantReader
            .GetAllAsync().GetAwaiter().GetResult();

        foreach (TenantData tenant in tenants)
        {
            using (_currentTenant.Change(tenant.Id, tenant.Name))
            using (WebhooksTenantDbContext tenantCtx = _tenantFactory.CreateDbContext())
            {
                results.AddRange(tenantCtx.WebhookDeliveryAttempts
                    .AsNoTracking()
                    .ToList());
            }
        }

        return results;
    }
}
