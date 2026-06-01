using Granit.Hostnames.Domain;

namespace Granit.Hostnames.Contracts;

/// <summary>Write side for <see cref="ManagedHostname"/> aggregates (CQRS command side).</summary>
public interface IManagedHostnameWriter
{
    /// <summary>Persists a new hostname. The globally unique host guards against hijacking.</summary>
    Task AddAsync(ManagedHostname hostname, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing hostname.</summary>
    Task UpdateAsync(ManagedHostname hostname, CancellationToken cancellationToken = default);

    /// <summary>Removes a hostname, freeing its host for re-registration.</summary>
    Task DeleteAsync(ManagedHostname hostname, CancellationToken cancellationToken = default);
}
