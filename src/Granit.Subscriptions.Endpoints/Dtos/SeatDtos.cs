namespace Granit.Subscriptions.Endpoints.Dtos;

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
