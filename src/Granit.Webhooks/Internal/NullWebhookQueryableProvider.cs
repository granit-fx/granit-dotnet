using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Default no-op implementation of <see cref="IWebhookQueryableProvider"/>.
/// Returns empty queryables. Replaced by <c>EfWebhookQueryableProvider</c> when
/// <c>Granit.Webhooks.EntityFrameworkCore</c> is loaded.
/// </summary>
internal sealed class NullWebhookQueryableProvider : IWebhookQueryableProvider
{
    public IQueryable<WebhookSubscription> GetSubscriptions() =>
        Enumerable.Empty<WebhookSubscription>().AsQueryable();

    public IQueryable<WebhookDeliveryAttempt> GetDeliveryAttempts() =>
        Enumerable.Empty<WebhookDeliveryAttempt>().AsQueryable();
}
