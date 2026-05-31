using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookDeliveryAttempt"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all delivery attempts are returned cross-tenant.
/// </summary>
internal sealed class EfWebhookDeliveryAttemptQueryableSource(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<WebhookDeliveryAttempt>
{
    private readonly WebhooksDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<WebhookDeliveryAttempt> GetQueryable()
    {
        IQueryable<WebhookDeliveryAttempt> query = _context.WebhookDeliveryAttempts.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
