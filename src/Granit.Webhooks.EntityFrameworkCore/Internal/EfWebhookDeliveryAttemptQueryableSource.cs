using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="WebhookDeliveryAttempt"/>.
/// </summary>
internal sealed class EfWebhookDeliveryAttemptQueryableSource(
    IDbContextFactory<WebhooksDbContext> contextFactory) : IQueryableSource<WebhookDeliveryAttempt>
{
    private readonly WebhooksDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<WebhookDeliveryAttempt> GetQueryable() =>
        _context.WebhookDeliveryAttempts.AsNoTracking();
}
