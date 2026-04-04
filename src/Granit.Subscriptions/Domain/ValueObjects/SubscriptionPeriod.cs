namespace Granit.Subscriptions.Domain.ValueObjects;

/// <summary>
/// Represents a subscription billing period with start/end dates and the anchor
/// used to calculate future period boundaries.
/// </summary>
/// <param name="Start">Start of the billing period.</param>
/// <param name="End">End of the billing period.</param>
/// <param name="BillingCycleAnchor">Anchor date for computing future period boundaries.</param>
public sealed record SubscriptionPeriod(
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset BillingCycleAnchor);
