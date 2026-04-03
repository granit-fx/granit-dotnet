using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions;

/// <summary>Reads plan data (query side of CQRS).</summary>
public interface IPlanReader
{
    /// <summary>Returns a plan by ID (including Archived plans).</summary>
    Task<Plan?> GetByIdAsync(PlanId id, CancellationToken cancellationToken = default);

    /// <summary>Returns all Published plans (excludes Draft and Archived).</summary>
    Task<IReadOnlyList<Plan>> GetAvailablePlansAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a plan by external provider mapping.</summary>
    Task<Plan?> GetByExternalIdAsync(
        string providerName,
        string externalId,
        CancellationToken cancellationToken = default);
}
