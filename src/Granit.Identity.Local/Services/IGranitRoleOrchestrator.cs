using Granit.Authorization.Domain;
using Granit.MultiTenancy;

namespace Granit.Identity.Local.Services;

/// <summary>
/// Coordinates the dual write between <c>GranitRole</c> (local identity) and
/// <see cref="RoleMetadata"/> (authorization scope metadata) for CRUD operations
/// exposed by the admin endpoints.
/// </summary>
/// <remarks>
/// <para>
/// GranitRole rows live in the Identity DbContext (e.g. <c>OpenIddictDbContext</c>) while
/// <see cref="RoleMetadata"/> rows live in the host DbContext that implements
/// <c>IPermissionGrantDbContext</c>. The orchestrator serialises the two writes and
/// performs a compensating delete of the GranitRole if the <see cref="RoleMetadata"/>
/// write fails, so the invariant "every Granit-managed role has matching metadata"
/// converges even without a cross-DbContext transaction.
/// </para>
/// <para>
/// Phase 1 uses this compensating-write approach. A future refinement can swap the
/// implementation for a shared-connection EF Core transaction when both DbContexts
/// target the same physical database, as documented in
/// <c>docs/dotnet/security/authorization-multitenancy-side.mdx</c>.
/// </para>
/// </remarks>
public interface IGranitRoleOrchestrator
{
    /// <summary>
    /// Creates a local <c>GranitRole</c> and its <see cref="RoleMetadata"/> row, raising
    /// a <c>RoleCreatedEvent</c> domain event on the aggregate.
    /// </summary>
    Task<RoleMetadata> CreateAsync(CreateRoleCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the role (both local and metadata side) and optionally updates its
    /// description. Side and tenant scope are immutable after creation.
    /// </summary>
    Task<RoleMetadata> RenameAsync(
        Guid roleId,
        string newName,
        string? newDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes the role. System roles (<see cref="RoleMetadata.IsSystem"/>) cannot
    /// be deleted through this path.
    /// </summary>
    Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
}

/// <summary>Input for <see cref="IGranitRoleOrchestrator.CreateAsync"/>.</summary>
/// <param name="Name">Role display name, e.g. <c>"Manager"</c>.</param>
/// <param name="MultiTenancySide">Declarative scope of the role.</param>
/// <param name="TenantId">Tenant identifier — must be set iff <paramref name="MultiTenancySide"/> is <see cref="Granit.MultiTenancy.MultiTenancySide.Tenant"/>.</param>
/// <param name="ClientId">Optional OIDC client scope (always <c>null</c> in Phase 1).</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsSystem">Mark the role as platform-provisioned (prevents CRUD via endpoints).</param>
public sealed record CreateRoleCommand(
    string Name,
    MultiTenancySide MultiTenancySide,
    Guid? TenantId,
    string? ClientId = null,
    string? Description = null,
    bool IsSystem = false);
