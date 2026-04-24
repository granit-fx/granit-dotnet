using Granit.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A scheduled segment of a <see cref="Subscription"/>'s timeline that pins the
/// active <see cref="PlanId"/> (and optional override price / discount) over a
/// half-open interval <c>[StartDate, EndDate)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Phases enable ramp deals — e.g. <c>"trial → standard at day 30 → enterprise
/// at day 365"</c>. The billing-cycle orchestrator consults
/// <see cref="Subscription.GetActivePhase"/> at billing time to pick the plan
/// that applies to "now"; outside any phase, the orchestrator falls back to
/// <see cref="Subscription.PlanId"/> for backward compatibility with single-plan
/// subscriptions.
/// </para>
/// <para>
/// Boundary semantics: <c>StartDate</c> is inclusive, <c>EndDate</c> is
/// exclusive. <c>EndDate = null</c> = "open until the next phase begins, or
/// forever if this is the last phase". Adjacent phases must touch without
/// overlap — the validator in <c>SubscriptionPhaseConventionValidator</c>
/// (HTTP layer) enforces both rules at the boundary.
/// </para>
/// </remarks>
public sealed class SubscriptionPhase : Entity
{
    private SubscriptionPhase() { }

    /// <summary>Creates a new phase scheduled to run from <paramref name="startDate"/> until <paramref name="endDate"/>.</summary>
    /// <param name="id">Phase id.</param>
    /// <param name="subscriptionId">Owning subscription (FK).</param>
    /// <param name="startDate">Inclusive lower bound (UTC).</param>
    /// <param name="endDate">Exclusive upper bound, or <c>null</c> for an open-ended terminal phase.</param>
    /// <param name="planId">Plan that applies during this phase.</param>
    /// <param name="overridePriceId">Optional pinned <c>PlanPrice</c> for the phase (otherwise uses the plan's current price).</param>
    /// <param name="discountPercent">Optional flat percentage applied at billing time (0–100).</param>
    public static SubscriptionPhase Create(
        Guid id,
        Guid subscriptionId,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        PlanId planId,
        Guid? overridePriceId = null,
        decimal? discountPercent = null)
    {
        ArgumentNullException.ThrowIfNull(planId);

        if (endDate is { } e && e <= startDate)
        {
            throw new ArgumentException(
                $"Phase end date {e:O} must be strictly greater than start {startDate:O}.",
                nameof(endDate));
        }

        if (discountPercent is { } d && (d < 0m || d > 100m))
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent),
                d, "DiscountPercent must be between 0 and 100 inclusive.");
        }

        return new SubscriptionPhase
        {
            Id = id,
            SubscriptionId = subscriptionId,
            StartDate = startDate,
            EndDate = endDate,
            PlanId = planId,
            OverridePriceId = overridePriceId,
            DiscountPercent = discountPercent,
        };
    }

    /// <summary>Owning subscription (FK).</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Inclusive lower bound of the phase (UTC).</summary>
    public DateTimeOffset StartDate { get; private set; }

    /// <summary>Exclusive upper bound; <c>null</c> = open-ended (terminal phase).</summary>
    public DateTimeOffset? EndDate { get; private set; }

    /// <summary>Plan that applies during this phase.</summary>
    public PlanId PlanId { get; private set; } = null!;

    /// <summary>Optional pinned price version for this phase.</summary>
    public Guid? OverridePriceId { get; private set; }

    /// <summary>Optional flat percentage discount applied at billing time (0–100).</summary>
    public decimal? DiscountPercent { get; private set; }

    /// <summary>
    /// Whether this phase covers the supplied instant. Half-open semantics:
    /// <c>StartDate ≤ instant &lt; EndDate</c>; an open-ended phase covers any
    /// instant ≥ <c>StartDate</c>.
    /// </summary>
    public bool Covers(DateTimeOffset instant) =>
        instant >= StartDate && (EndDate is null || instant < EndDate.Value);
}
