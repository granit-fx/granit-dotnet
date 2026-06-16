using Granit.Authorization.Domain;

namespace Granit.Authorization;

/// <summary>
/// Persistence abstraction for <see cref="RoleMetadata"/>.
/// </summary>
/// <remarks>
/// <para>
/// Consumers (the grant validator, CRUD endpoints, orchestrator) depend on this
/// contract so roles defined by the local identity store and by future federated
/// providers share the same metadata surface.
/// </para>
/// <para>
/// <b>Orchestrator atomic path caveat.</b> When <c>IGranitRoleOrchestrator</c> takes
/// its shared-connection atomic path (see ADR-024), the write methods on this
/// interface (<see cref="AddAsync"/>, <see cref="UpdateAsync"/>, <see cref="RemoveAsync"/>)
/// are <b>bypassed by design</b> — the orchestrator writes directly through a fresh
/// <c>DbContext</c> attached to the shared transaction. Implementations that want to
/// run additional business logic (in-memory event publication, extra validation,
/// cross-cutting audit) around role persistence MUST NOT rely on being called
/// through this contract; extract such logic into a domain service invoked by the
/// orchestrator itself (or by the CRUD endpoint layer before orchestration) so it
/// runs on both paths.
/// </para>
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

    /// <summary>
    /// Returns the roles scoped to the given OIDC <paramref name="clientId"/>. Pass
    /// <see langword="null"/> to list realm-scoped rows (<c>ClientId IS NULL</c>).
    /// </summary>
    /// <remarks>
    /// Used by the client-role sync (ADR-029) to compute the per-client orphan set —
    /// the rows that were in the store last pass but are no longer returned by the
    /// upstream provider.
    /// </remarks>
    Task<IReadOnlyList<RoleMetadata>> ListByClientIdAsync(
        string? clientId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new role metadata row.</summary>
    Task AddAsync(RoleMetadata role, CancellationToken cancellationToken = default);

    /// <summary>Persists modifications applied to a tracked <see cref="RoleMetadata"/>.</summary>
    /// <param name="role">Modified aggregate.</param>
    /// <param name="concurrencyStamp">When provided, overrides the EF Core original-value for the concurrency token so a mismatch throws a concurrency exception (→ HTTP 409).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(RoleMetadata role, string? concurrencyStamp = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a role metadata row.</summary>
    Task RemoveAsync(RoleMetadata role, CancellationToken cancellationToken = default);
}
