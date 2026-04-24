namespace Granit.Subscriptions.Domain;

/// <summary>
/// Type of <see cref="SubscriptionDiscount"/> applied to a subscription's invoices.
/// </summary>
public enum DiscountType
{
    /// <summary>
    /// Reduces the running invoice total by <c>Value</c> percent (0–100). Stacks
    /// cumulatively when multiple Percentage discounts are active — each applies
    /// to whatever amount remains after previous discounts in the declaration order.
    /// </summary>
    Percentage = 0,

    /// <summary>
    /// Subtracts <c>Value</c> currency units from the running invoice total. The
    /// total is floored at 0 — a discount cannot produce a negative invoice.
    /// </summary>
    FixedAmount = 1,

    /// <summary>
    /// Extends the trial period by <c>Value</c> days. Has no immediate price
    /// impact at billing time (the calculator skips Trial entries); the trial
    /// extension is consumed by the trial-end scheduler in a separate path.
    /// </summary>
    Trial = 2,
}
