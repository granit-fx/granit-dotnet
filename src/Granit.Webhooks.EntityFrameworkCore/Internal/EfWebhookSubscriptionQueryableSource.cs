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
    ICurrentTenant currentTenant)
    : IQueryableSource<WebhookSubscription>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private WebhooksDbContext? _context;

    public IQueryable<WebhookSubscription> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<WebhookSubscription> query = _context.WebhookSubscriptions.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        WebhooksDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
