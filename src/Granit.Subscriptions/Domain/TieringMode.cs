namespace Granit.Subscriptions.Domain;

/// <summary>
/// How a tiered <see cref="PlanPrice"/> applies its <see cref="PricingTier"/> brackets
/// to a billed quantity. Mirrors the two ORB/Stripe modes.
/// </summary>
public enum TieringMode
{
    /// <summary>
    /// The unit price of the highest tier reached applies to the entire quantity.
    /// Example with tiers [10K @ $0.10, 100K @ $0.05, ∞ @ $0.02]: 50K calls cost
    /// <c>50_000 × 0.05 = $2_500</c> (all 50 000 charged at the second tier's rate).
    /// </summary>
    Volume = 0,

    /// <summary>
    /// Each tier's unit price applies only to the units that fall within that tier
    /// — the bracket model. Example with tiers [10K @ $0.10, 100K @ $0.05, ∞ @ $0.02]:
    /// 50K calls cost <c>10_000 × 0.10 + 40_000 × 0.05 = $3_000</c>.
    /// </summary>
    Graduated = 1,
}
