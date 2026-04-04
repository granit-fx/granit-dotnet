using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A price point for a plan in a specific currency and billing interval.
/// Supports versioning: when a price is replaced, <see cref="ReplacedByPriceId"/>
/// points to the successor and <see cref="ReplacedAt"/> records the timestamp.
/// </summary>
public sealed class PlanPrice : Entity
{
    private PlanPrice() { }

    /// <summary>Creates a new plan price.</summary>
    public static PlanPrice Create(
        Guid id, decimal amount, string currency, BillingInterval interval,
        DateTimeOffset effectiveFrom) =>
        new()
        {
            Id = id,
            Amount = amount,
            Currency = currency,
            Interval = interval,
            EffectiveFrom = effectiveFrom,
        };

    /// <summary>Price amount in the smallest currency unit (e.g., cents).</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code (e.g., "EUR", "USD").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Billing interval this price applies to.</summary>
    public BillingInterval Interval { get; private set; }

    /// <summary>When this price version became effective.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>ID of the price version that replaced this one. Null if this is the current version.</summary>
    public Guid? ReplacedByPriceId { get; private set; }

    /// <summary>When this price was replaced by a newer version. Null if still current.</summary>
    public DateTimeOffset? ReplacedAt { get; private set; }

    /// <summary>Whether this price is the current active version (not replaced).</summary>
    public bool IsActive => ReplacedByPriceId is null;

    /// <summary>Marks this price as replaced by a newer version.</summary>
    internal void MarkReplaced(Guid replacedByPriceId, DateTimeOffset replacedAt)
    {
        if (ReplacedByPriceId is not null)
        {
            throw new InvalidOperationException(
                $"Plan price '{Id}' has already been replaced by '{ReplacedByPriceId}'.");
        }

        ReplacedByPriceId = replacedByPriceId;
        ReplacedAt = replacedAt;
    }
}
