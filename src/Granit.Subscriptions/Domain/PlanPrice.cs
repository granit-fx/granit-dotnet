using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A price point for a plan in a specific currency and billing interval.
/// </summary>
public sealed class PlanPrice : Entity
{
    private PlanPrice() { }

    /// <summary>Creates a new plan price.</summary>
    public static PlanPrice Create(Guid id, decimal amount, string currency, BillingInterval interval) =>
        new()
        {
            Id = id,
            Amount = amount,
            Currency = currency,
            Interval = interval,
        };

    /// <summary>Price amount in the smallest currency unit (e.g., cents).</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code (e.g., "EUR", "USD").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Billing interval this price applies to.</summary>
    public BillingInterval Interval { get; private set; }
}
