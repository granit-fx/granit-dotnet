using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Provides <see cref="IQueryable{T}"/> access to webhook entities for use by
/// <c>Granit.QueryEngine</c> query endpoints.
/// </summary>
/// <remarks>
/// This is <b>not</b> a repository — it exposes raw queryables for the query engine.
/// All filtering, sorting, and pagination logic lives in <see cref="Granit.QueryEngine"/>.
/// The default no-op implementation returns empty queryables; the
/// <c>Granit.Webhooks.EntityFrameworkCore</c> package replaces it with DbContext-backed sources.
/// </remarks>
public interface IWebhookQueryableProvider
{
    /// <summary>Returns a queryable source for <see cref="WebhookSubscription"/> entities.</summary>
    IQueryable<WebhookSubscription> GetSubscriptions();

    /// <summary>Returns a queryable source for <see cref="WebhookDeliveryAttempt"/> entities.</summary>
    IQueryable<WebhookDeliveryAttempt> GetDeliveryAttempts();
}
