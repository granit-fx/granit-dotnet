using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions;

/// <summary>Persists plan changes (command side of CQRS).</summary>
public interface IPlanWriter
{
    /// <summary>Persists a new plan.</summary>
    Task AddAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing plan.</summary>
    Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default);
}
