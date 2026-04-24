using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Pricing;

/// <summary>
/// Pure computation of the total amount for a tiered <see cref="PlanPrice"/>.
/// </summary>
/// <remarks>
/// Boundary semantics: a tier <em>contains</em> its <see cref="PricingTier.UpToQuantity"/>.
/// A quantity exactly equal to the bracket boundary belongs to the lower tier — i.e.
/// the bracket interval is <c>(previousUpTo, currentUpTo]</c>. The first bracket
/// starts at <c>0</c> exclusive (a positive quantity is required for any cost to
/// apply).
/// </remarks>
public static class TieredPricingCalculator
{
    /// <summary>
    /// Computes the total amount owed for <paramref name="quantity"/> units billed
    /// against <paramref name="tiers"/> under the supplied <paramref name="mode"/>.
    /// </summary>
    /// <param name="quantity">Total billable quantity (must be ≥ 0).</param>
    /// <param name="mode">Tier semantics.</param>
    /// <param name="tiers">Tier sequence (will be sorted ascending by <c>SortOrder</c>).</param>
    /// <returns>The total amount, or <c>0</c> when <paramref name="quantity"/> is 0 or the tier list is empty.</returns>
    public static decimal Compute(
        decimal quantity,
        TieringMode mode,
        IReadOnlyList<PricingTier> tiers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentNullException.ThrowIfNull(tiers);

        if (quantity == 0m || tiers.Count == 0)
        {
            return 0m;
        }

        // Defensive — never trust the caller's ordering even though SetTiers also normalises.
        IReadOnlyList<PricingTier> ordered = [.. tiers.OrderBy(t => t.SortOrder)];

        return mode switch
        {
            TieringMode.Volume => ComputeVolume(quantity, ordered),
            TieringMode.Graduated => ComputeGraduated(quantity, ordered),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown tiering mode."),
        };
    }

    private static decimal ComputeVolume(decimal quantity, IReadOnlyList<PricingTier> tiers)
    {
        // Volume: find the active tier (the one whose UpToQuantity covers the quantity)
        // and apply its unit price to the entire quantity. The open-ended last tier
        // covers any quantity above all earlier brackets.
        foreach (PricingTier tier in tiers)
        {
            if (tier.UpToQuantity is null || quantity <= tier.UpToQuantity.Value)
            {
                return (quantity * tier.UnitAmount) + (tier.FlatAmount ?? 0m);
            }
        }

        // Should not be reachable when the last tier is open-ended (validator guarantees it),
        // but fall through defensively to the last tier's rate rather than throwing.
        PricingTier last = tiers[^1];
        return (quantity * last.UnitAmount) + (last.FlatAmount ?? 0m);
    }

    private static decimal ComputeGraduated(decimal quantity, IReadOnlyList<PricingTier> tiers)
    {
        // Graduated: walk the brackets in order, charge each one for the units that
        // fall into its (previousUpTo, currentUpTo] interval, plus the optional flat
        // fee per crossed bracket.
        decimal remaining = quantity;
        decimal previousBoundary = 0m;
        decimal total = 0m;

        foreach (PricingTier tier in tiers)
        {
            if (remaining <= 0m)
            {
                break;
            }

            decimal capacity = tier.UpToQuantity is null
                ? remaining
                : Math.Max(0m, tier.UpToQuantity.Value - previousBoundary);

            decimal unitsInTier = Math.Min(remaining, capacity);
            if (unitsInTier > 0m)
            {
                total += (unitsInTier * tier.UnitAmount) + (tier.FlatAmount ?? 0m);
                remaining -= unitsInTier;
                previousBoundary = tier.UpToQuantity ?? previousBoundary + unitsInTier;
            }
        }

        return total;
    }
}
