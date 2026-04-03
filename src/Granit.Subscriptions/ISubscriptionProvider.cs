using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions;

/// <summary>
/// Abstracts subscription lifecycle operations against an external provider.
/// </summary>
/// <remarks>
/// <para>
/// Granit is the source of truth for subscription status. Providers are executors
/// that sync state to/from external systems. Providers with capabilities like
/// <see cref="SubscriptionProviderCapabilities.Dunning"/> can initiate transitions
/// via webhooks.
/// </para>
/// <para>
/// The Internal provider (self-hosted) returns success for all operations since
/// there is no external system to sync with. The Stripe provider delegates to
/// Stripe's Billing API.
/// </para>
/// </remarks>
public interface ISubscriptionProvider
{
    /// <summary>Provider name (e.g., <c>"internal"</c>, <c>"stripe"</c>).</summary>
    string Name { get; }

    /// <summary>Capabilities this provider manages.</summary>
    SubscriptionProviderCapabilities Capabilities { get; }

    /// <summary>Creates the subscription in the external system.</summary>
    /// <returns>The provider name and external identifier for mapping storage.</returns>
    Task<(string ProviderName, string ExternalId)> CreateExternalAsync(
        Subscription subscription,
        Plan plan,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the subscription in the external system.</summary>
    Task CancelExternalAsync(
        SubscriptionExternalMapping mapping,
        bool atPeriodEnd,
        CancellationToken cancellationToken = default);

    /// <summary>Changes the plan in the external system.</summary>
    Task ChangePlanExternalAsync(
        SubscriptionExternalMapping mapping,
        Plan newPlan,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the seat count in the external system.</summary>
    Task UpdateSeatsExternalAsync(
        SubscriptionExternalMapping mapping,
        int quantity,
        CancellationToken cancellationToken = default);
}
