using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A customer-negotiated price that supersedes the standard
/// <see cref="PlanPrice.Amount"/> for a single <see cref="Subscription"/> over
/// a half-open time window <c>[EffectiveFrom, EffectiveUntil)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Overrides target a specific <see cref="PlanPriceId"/> — typically the
/// subscription's pinned <see cref="Subscription.PlanPriceId"/>, or the current
/// active price resolved at billing time. Multiple overrides may exist for the
/// same <c>PlanPriceId</c> across non-overlapping time windows (e.g. a special
/// rate for the first 6 months, then a different one); the orchestrator picks
/// the one whose window covers <c>now</c>.
/// </para>
/// <para>
/// Currency is implicitly carried by the parent <see cref="PlanPrice"/> — the
/// override's <see cref="Amount"/> is denominated in the same currency.
/// </para>
/// </remarks>
public sealed class SubscriptionPriceOverride : Entity
{
    private SubscriptionPriceOverride() { }

    /// <summary>Maximum length of the free-text reason captured at creation.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>Creates a new override attached to the supplied subscription.</summary>
    /// <param name="id">Override id.</param>
    /// <param name="subscriptionId">Owning subscription (FK).</param>
    /// <param name="planPriceId">Target <see cref="PlanPrice"/> whose <c>Amount</c> is overridden.</param>
    /// <param name="amount">Negotiated amount in the parent price's currency (≥ 0).</param>
    /// <param name="effectiveFrom">Inclusive lower bound of the override window (UTC).</param>
    /// <param name="effectiveUntil">Exclusive upper bound; <c>null</c> = open-ended.</param>
    /// <param name="reason">Free-text justification (commercial agreement reference, ≤ 500 chars).</param>
    public static SubscriptionPriceOverride Create(
        Guid id,
        Guid subscriptionId,
        Guid planPriceId,
        decimal amount,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (effectiveUntil is { } u && u <= effectiveFrom)
        {
            throw new ArgumentException(
                $"Override effectiveUntil {u:O} must be strictly greater than effectiveFrom {effectiveFrom:O}.",
                nameof(effectiveUntil));
        }

        return new SubscriptionPriceOverride
        {
            Id = id,
            SubscriptionId = subscriptionId,
            PlanPriceId = planPriceId,
            Amount = amount,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            Reason = reason,
        };
    }

    /// <summary>Owning subscription (FK).</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Target <see cref="PlanPrice"/> id whose <c>Amount</c> is overridden.</summary>
    public Guid PlanPriceId { get; private set; }

    /// <summary>Negotiated amount, in the parent <see cref="PlanPrice"/>'s currency.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Inclusive lower bound of the override window.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>Exclusive upper bound; <c>null</c> = open-ended.</summary>
    public DateTimeOffset? EffectiveUntil { get; private set; }

    /// <summary>Free-text justification (commercial agreement reference).</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this override covers the supplied instant for the supplied price id.
    /// Half-open semantics: <c>EffectiveFrom ≤ instant &lt; EffectiveUntil</c>.
    /// </summary>
    public bool Covers(Guid planPriceId, DateTimeOffset instant) =>
        PlanPriceId == planPriceId
        && instant >= EffectiveFrom
        && (EffectiveUntil is null || instant < EffectiveUntil.Value);
}
