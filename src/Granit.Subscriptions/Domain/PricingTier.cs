using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A single bracket of a tiered <see cref="PlanPrice"/>. The semantics
/// (Volume vs Graduated) are carried by the parent's <see cref="PlanPrice.TieringMode"/>.
/// </summary>
/// <remarks>
/// Tiers are ordered by <see cref="SortOrder"/> ascending (= bracket index from
/// cheapest threshold to highest). The last tier in the ordered sequence MUST have
/// <see cref="UpToQuantity"/> = <c>null</c> — it represents the open-ended "above
/// all previous brackets" range. Validation in
/// <c>PricingTierConventionValidator</c> enforces both rules.
/// </remarks>
public sealed class PricingTier : Entity
{
    private PricingTier() { }

    /// <summary>Creates a new tier bracket attached to a plan price.</summary>
    /// <param name="id">Tier id (server-generated).</param>
    /// <param name="planPriceId">Owning <see cref="PlanPrice"/>.</param>
    /// <param name="sortOrder">Position in the bracket sequence (0 = cheapest threshold).</param>
    /// <param name="upToQuantity">Inclusive upper bound of this tier; <c>null</c> = ∞ (last tier only).</param>
    /// <param name="unitAmount">Per-unit price applied within this tier (or to the whole quantity in Volume mode).</param>
    /// <param name="flatAmount">
    /// Optional fixed fee charged when the bracket is hit (regardless of unit count).
    /// Used for "package" pricing — e.g. "$50 plus $0.01/call up to 5K". Always
    /// added on top of the unit-price computation.
    /// </param>
    public static PricingTier Create(
        Guid id,
        Guid planPriceId,
        int sortOrder,
        decimal? upToQuantity,
        decimal unitAmount,
        decimal? flatAmount = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
        ArgumentOutOfRangeException.ThrowIfNegative(unitAmount);
        if (upToQuantity is { } u)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(u);
        }
        if (flatAmount is { } f)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(f);
        }

        return new PricingTier
        {
            Id = id,
            PlanPriceId = planPriceId,
            SortOrder = sortOrder,
            UpToQuantity = upToQuantity,
            UnitAmount = unitAmount,
            FlatAmount = flatAmount,
        };
    }

    /// <summary>Owning <see cref="PlanPrice"/> (FK).</summary>
    public Guid PlanPriceId { get; private set; }

    /// <summary>Position in the bracket sequence — 0 is the cheapest-threshold tier.</summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Inclusive upper bound of this tier. Quantities equal to this value still
    /// belong to this tier (the tier <em>contains</em> its boundary). <c>null</c>
    /// = open-ended (must be true for the last tier and only the last tier).
    /// </summary>
    public decimal? UpToQuantity { get; private set; }

    /// <summary>Per-unit price applied to units within this tier.</summary>
    public decimal UnitAmount { get; private set; }

    /// <summary>
    /// Optional flat package fee charged when the tier is reached (Graduated:
    /// charged per crossed bracket; Volume: charged once for the active bracket).
    /// </summary>
    public decimal? FlatAmount { get; private set; }
}
