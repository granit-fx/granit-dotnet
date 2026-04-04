namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Request to create a new subscription.</summary>
public sealed record SubscriptionCreateRequest(
    Guid PlanId,
    string Currency,
    DateTimeOffset? TrialEndsAt = null);

/// <summary>Request to cancel a subscription.</summary>
public sealed record SubscriptionCancelRequest(
    string? Reason = null,
    bool AtPeriodEnd = false);

/// <summary>Request to change subscription plan.</summary>
public sealed record SubscriptionChangePlanRequest(
    Guid NewPlanId);

/// <summary>Request to assign a seat.</summary>
public sealed record SeatAssignRequest(
    Guid UserId);

/// <summary>Seat assignment details.</summary>
public sealed record SeatResponse(
    Guid Id, Guid UserId, DateTimeOffset AssignedAt)
{
    internal static SeatResponse FromEntity(Domain.SubscriptionSeat seat) =>
        new(seat.Id, seat.UserId, seat.AssignedAt);
}
