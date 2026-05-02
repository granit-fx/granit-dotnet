using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A seat assignment linking a user to a subscription.
/// </summary>
public sealed class SubscriptionSeat : Entity
{
    private SubscriptionSeat() { }

    /// <summary>Creates a new seat assignment.</summary>
    public static SubscriptionSeat Create(Guid id, Guid userId, DateTimeOffset assignedAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            AssignedAt = assignedAt,
        };

    /// <summary>
    /// The canonical <see cref="Granit.Identity.Domain.User.Id"/>
    /// assigned to this seat per ADR-051. The same Guid resolves
    /// both <c>LocalIdentity</c> and <c>FederatedIdentity</c> rows
    /// when the alignment guarantee holds, so a seat is satisfied
    /// regardless of the user's login path.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>When the seat was assigned.</summary>
    public DateTimeOffset AssignedAt { get; private set; }
}
