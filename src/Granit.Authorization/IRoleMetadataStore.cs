using Granit.Authorization.Domain;

namespace Granit.Authorization;

/// <summary>
/// Persistence abstraction for <see cref="RoleMetadata"/>.
/// </summary>
/// <remarks>
/// Consumers (the grant validator, CRUD endpoints, orchestrator) depend on this
/// contract so roles defined by the local identity store and by future federated
/// providers share the same metadata surface.
/// </remarks>
public interface IRoleMetadataStore
{
    /// <summary>Returns the role with the given identifier, or <see langword="null"/> if absent.</summary>
    Task<RoleMetadata?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the role matching the given <paramref name="name"/>, <paramref name="tenantId"/>
    /// and <paramref name="clientId"/> triplet, or <see langword="null"/> if absent.
    /// </summary>
    Task<RoleMetadata?> FindByNameAsync(
        string name,
        Guid? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every role stored in the backing provider. Caller is expected to filter
    /// for visibility — this method performs no tenant scoping.
    /// </summary>
    Task<IReadOnlyList<RoleMetadata>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a new role metadata row.</summary>
    Task AddAsync(RoleMetadata role, CancellationToken cancellationToken = default);

    /// <summary>Persists modifications applied to a tracked <see cref="RoleMetadata"/>.</summary>
    Task UpdateAsync(RoleMetadata role, CancellationToken cancellationToken = default);

    /// <summary>Removes a role metadata row.</summary>
    Task RemoveAsync(RoleMetadata role, CancellationToken cancellationToken = default);
}
