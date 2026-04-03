using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>Persists seat assignment changes (command side of CQRS).</summary>
public interface ISeatWriter
{
    /// <summary>Assigns a seat to a user within a subscription.</summary>
    /// <exception cref="Granit.Features.Exceptions.FeatureLimitExceededException">
    /// Thrown when the seat limit for the plan is reached.
    /// </exception>
    Task AssignSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a seat from a user within a subscription.</summary>
    /// <returns><c>true</c> if the seat was found and revoked; <c>false</c> if the user had no seat.</returns>
    Task<bool> RevokeSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
