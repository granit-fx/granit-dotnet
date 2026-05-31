using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookSubscription"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all webhook subscriptions are returned cross-tenant.
/// </summary>
internal sealed class EfWebhookSubscriptionQueryableSource(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<WebhookSubscription>
{
    private readonly WebhooksDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<WebhookSubscription> GetQueryable()
    {
        IQueryable<WebhookSubscription> query = _context.WebhookSubscriptions.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
