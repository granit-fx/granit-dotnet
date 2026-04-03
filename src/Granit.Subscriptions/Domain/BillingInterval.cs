namespace Granit.Subscriptions.Domain;

/// <summary>
/// Billing interval for a subscription plan.
/// </summary>
public enum BillingInterval
{
    /// <summary>Billed every month.</summary>
    Monthly = 0,

    /// <summary>Billed every 3 months.</summary>
    Quarterly = 1,

    /// <summary>Billed every 12 months.</summary>
    Yearly = 2,
}
