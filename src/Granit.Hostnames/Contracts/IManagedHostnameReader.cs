using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Contracts;

/// <summary>Read side for <see cref="ManagedHostname"/> aggregates (CQRS query side).</summary>
public interface IManagedHostnameReader
{
    /// <summary>Loads a hostname by id, or <c>null</c> when absent.</summary>
    Task<ManagedHostname?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a hostname by its host value (case-insensitive), tenant-agnostically, or <c>null</c>.
    /// </summary>
    Task<ManagedHostname?> FindByHostAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>Lists the hostnames registered for an owning resource.</summary>
    Task<IReadOnlyList<ManagedHostname>> ListByOwnerAsync(
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken = default);
}
