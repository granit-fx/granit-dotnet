using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A negotiated discount applied to a <see cref="Subscription"/>'s invoices —
/// percentage off, fixed-amount reduction, or trial-period extension.
/// </summary>
/// <remarks>
/// Multiple discounts on the same subscription stack cumulatively in declaration
/// order: each price-impacting discount applies to the running total after the
/// previous one. Trial discounts have no price impact at billing time (the
/// calculator skips them); they are consumed by the trial-end scheduler.
/// </remarks>
public sealed class SubscriptionDiscount : Entity
{
    private SubscriptionDiscount() { }

    /// <summary>Maximum length of the free-text reason captured at creation.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>Creates a new discount attached to the supplied subscription.</summary>
    /// <param name="id">Discount id.</param>
    /// <param name="subscriptionId">Owning subscription (FK).</param>
    /// <param name="type">Discount type.</param>
    /// <param name="value">
    /// Type-dependent magnitude:
    /// <list type="bullet">
    ///   <item><see cref="DiscountType.Percentage"/>: percent off, 0–100 inclusive.</item>
    ///   <item><see cref="DiscountType.FixedAmount"/>: currency units to subtract, ≥ 0.</item>
    ///   <item><see cref="DiscountType.Trial"/>: integer days to add to the trial period (≥ 0).</item>
    /// </list>
    /// </param>
    /// <param name="reason">Free-text justification (commercial agreement reference, ≤ 500 chars).</param>
    /// <param name="expiresAt">Optional UTC instant past which the discount stops being applied.</param>
    public static SubscriptionDiscount Create(
        Guid id,
        Guid subscriptionId,
        DiscountType type,
        decimal value,
        string reason,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (type == DiscountType.Percentage && value > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "Percentage discount must be in [0, 100].");
        }

        if (type == DiscountType.Trial && value != decimal.Truncate(value))
        {
            throw new ArgumentException("Trial discount value must be a whole number of days.", nameof(value));
        }

        return new SubscriptionDiscount
        {
            Id = id,
            SubscriptionId = subscriptionId,
            Type = type,
            Value = value,
            Reason = reason,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Owning subscription (FK).</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Discount kind (Percentage / FixedAmount / Trial).</summary>
    public DiscountType Type { get; private set; }

    /// <summary>
    /// Type-dependent magnitude: percent for <see cref="DiscountType.Percentage"/>,
    /// currency units for <see cref="DiscountType.FixedAmount"/>, days for
    /// <see cref="DiscountType.Trial"/>.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>Free-text justification (commercial agreement reference).</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional UTC instant past which the discount stops being applied.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>True when the discount applies to invoices generated at <paramref name="instant"/>.</summary>
    public bool IsActiveAt(DateTimeOffset instant) =>
        ExpiresAt is null || instant < ExpiresAt.Value;
}
