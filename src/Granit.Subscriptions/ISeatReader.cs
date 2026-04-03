using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>Reads seat assignment data (query side of CQRS).</summary>
public interface ISeatReader
{
    /// <summary>Returns the number of assigned seats for a subscription.</summary>
    Task<int> GetSeatCountAsync(SubscriptionId subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all assigned seats for a subscription.</summary>
    Task<IReadOnlyList<SubscriptionSeat>> GetSeatsAsync(
        SubscriptionId subscriptionId,
        CancellationToken cancellationToken = default);
}
