using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookSubscription"/>.
/// </summary>
internal sealed class EfWebhookSubscriptionQueryableSource(
    IDbContextFactory<WebhooksDbContext> contextFactory) : IQueryableSource<WebhookSubscription>
{
    private readonly WebhooksDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<WebhookSubscription> GetQueryable() =>
        _context.WebhookSubscriptions.AsNoTracking();
}
