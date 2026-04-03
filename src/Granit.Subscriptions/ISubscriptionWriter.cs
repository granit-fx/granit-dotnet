using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions;

/// <summary>Persists subscription changes (command side of CQRS).</summary>
public interface ISubscriptionWriter
{
    /// <summary>Persists a new subscription.</summary>
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing subscription.</summary>
    Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
}
