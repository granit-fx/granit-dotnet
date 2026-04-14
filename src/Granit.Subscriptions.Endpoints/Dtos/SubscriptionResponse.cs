using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Subscription details.</summary>
public sealed record SubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string Status,
    string Currency,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset? TrialEndsAt,
    bool CancelAtPeriodEnd,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    int SeatCount,
    Guid? PlanPriceId = null)
{
    internal static SubscriptionResponse FromEntity(Subscription sub) => new(
        sub.Id,
        sub.PlanId,
        sub.Status.ToString(),
        sub.Currency,
        sub.CurrentPeriodStart,
        sub.CurrentPeriodEnd,
        sub.TrialEndsAt,
        sub.CancelAtPeriodEnd,
        sub.CancelledAt,
        sub.CancellationReason,
        sub.Seats.Count,
        sub.PlanPriceId);
}
