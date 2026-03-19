using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookQueryableProvider"/>.
/// Provides <see cref="IQueryable{T}"/> access to webhook entities for the query engine.
/// </summary>
internal sealed class EfWebhookQueryableProvider(
    IDbContextFactory<WebhooksDbContext> contextFactory) : IWebhookQueryableProvider
{
    // Cache contexts per-request to avoid multiple factory calls.
    // The provider is scoped, so these will be disposed at the end of the request.
    private WebhooksDbContext? _context;

    public IQueryable<WebhookSubscription> GetSubscriptions() =>
        GetContext().WebhookSubscriptions.AsNoTracking();

    public IQueryable<WebhookDeliveryAttempt> GetDeliveryAttempts() =>
        GetContext().WebhookDeliveryAttempts.AsNoTracking();

    private WebhooksDbContext GetContext() =>
        _context ??= contextFactory.CreateDbContext();
}
