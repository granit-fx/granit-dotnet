using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Pricing;

/// <summary>
/// Applies a sequence of <see cref="SubscriptionDiscount"/> entries to a base
/// invoice amount. Pure function — no DB / DI / I/O.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Stacking order:</strong> discounts are applied in iteration order
/// (typically declaration order = insertion order on the parent
/// <see cref="Subscription"/>). Each discount applies to the running total
/// after the previous one — the model is <em>cumulative</em>, not
/// <em>exclusive</em>. Two 10 % discounts produce a 19 % effective discount,
/// not 20 %, because the second 10 % is computed on the post-first-discount total.
/// </para>
/// <para>
/// <strong>Floor at 0:</strong> the result is clamped to ≥ 0 — no
/// <see cref="DiscountType.FixedAmount"/> can produce a negative invoice.
/// </para>
/// <para>
/// <strong>Trial discounts are skipped</strong> — they extend the trial period
/// (consumed elsewhere) and have no immediate billing impact.
/// </para>
/// </remarks>
public static class SubscriptionDiscountCalculator
{
    /// <summary>Returns the amount after applying every discount in order.</summary>
    /// <param name="baseAmount">Amount before discounts (≥ 0).</param>
    /// <param name="discounts">Discounts to apply, in stacking order.</param>
    /// <param name="now">Reference instant for <see cref="SubscriptionDiscount.IsActiveAt"/>.</param>
    /// <returns>Discounted amount, ≥ 0.</returns>
    public static decimal Apply(
        decimal baseAmount,
        IEnumerable<SubscriptionDiscount> discounts,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseAmount);
        ArgumentNullException.ThrowIfNull(discounts);

        decimal total = baseAmount;

        foreach (SubscriptionDiscount discount in discounts)
        {
            if (!discount.IsActiveAt(now))
            {
                continue;
            }

            total = discount.Type switch
            {
                DiscountType.Percentage => decimal.Round(
                    total * (1m - (discount.Value / 100m)),
                    decimals: 4,
                    MidpointRounding.ToEven),
                DiscountType.FixedAmount => Math.Max(0m, total - discount.Value),
                DiscountType.Trial => total,   // No price impact at billing time.
                _ => total,
            };
        }

        return total;
    }
}
