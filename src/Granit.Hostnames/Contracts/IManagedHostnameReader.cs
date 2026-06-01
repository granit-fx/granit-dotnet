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

    /// <summary>
    /// Lists hostnames that are due for a DNS check: status is <see cref="HostnameStatus.Verifying"/>
    /// or <see cref="HostnameStatus.Error"/>, and <c>NextCheckAt ≤ <paramref name="now"/></c>.
    /// Dormant domains (<c>NextCheckAt</c> is <c>null</c>) are excluded — they require a
    /// manual <c>RequestRecheck</c>.
    /// </summary>
    /// <param name="now">Current timestamp; only entries due by this time are returned.</param>
    /// <param name="batchSize">Maximum number of hostnames to return per invocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ManagedHostname>> ListDueForVerificationAsync(
        DateTimeOffset now,
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}
