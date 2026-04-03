namespace Granit.Subscriptions;

/// <summary>
/// Capabilities declared by a subscription provider.
/// Determines which lifecycle events the provider manages vs Granit handling locally.
/// </summary>
[Flags]
public enum SubscriptionProviderCapabilities
{
    /// <summary>No capabilities — Granit handles everything locally.</summary>
    None = 0,

    /// <summary>Provider manages trial → active transition.</summary>
    Trials = 1,

    /// <summary>Provider manages payment retry (past_due → active).</summary>
    Dunning = 2,

    /// <summary>Provider calculates proration on plan changes.</summary>
    Proration = 4,

    /// <summary>Provider manages billing cycle and invoicing.</summary>
    BillingCycle = 8,
}
