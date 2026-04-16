using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>Persists seat assignment changes (command side of CQRS).</summary>
public interface ISeatWriter
{
    /// <summary>Assigns a seat to a user within a subscription.</summary>
    /// <returns>The created seat assignment.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the user already has a seat or the seat limit is reached.
    /// </exception>
    Task<SubscriptionSeat> AssignSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        int? seatLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a seat from a user within a subscription.</summary>
    /// <returns><c>true</c> if the seat was found and revoked; <c>false</c> if the user had no seat.</returns>
    Task<bool> RevokeSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
