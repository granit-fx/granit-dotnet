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
/// <c>IPermissionGrantDbContext</c>. The default implementation serialises the two writes
/// and performs a compensating delete of the GranitRole if the <see cref="RoleMetadata"/>
/// write fails, so the invariant "every Granit-managed role has matching metadata"
/// converges even without a cross-DbContext transaction.
/// </para>
/// <para>
/// A shared-connection EF Core transaction is preferable when both DbContexts target the
/// same physical database, but requires the Identity DbContext to expose its
/// <c>DbConnection</c> across assemblies — the current <c>OpenIddictDbContext</c> is
/// <c>internal sealed</c>. Implementers that control both contexts can register an
/// alternative implementation of this interface that opens a shared transaction instead.
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
    /// <param name="roleId">Identifier of the role to rename.</param>
    /// <param name="newName">New display name.</param>
    /// <param name="newDescription">New description, or <see langword="null"/> to clear.</param>
    /// <param name="concurrencyStamp">Stamp from the client's last read; must match the stored value — mismatch throws a concurrency exception (→ HTTP 409).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RoleMetadata> RenameAsync(
        Guid roleId,
        string newName,
        string? newDescription,
        string concurrencyStamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes the role. System roles (<see cref="RoleMetadata.IsSystem"/>) cannot
    /// be deleted through this path.
    /// </summary>
    Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
}

/// <summary>Input for <see cref="IGranitRoleOrchestrator.CreateAsync"/>.</summary>
/// <param name="Name">Role display name, e.g. <c>"Manager"</c>.</param>
/// <param name="MultiTenancySides">Declarative scope of the role.</param>
/// <param name="TenantId">Tenant identifier — must be set iff <paramref name="MultiTenancySides"/> is <see cref="Granit.MultiTenancy.MultiTenancySides.Tenant"/>.</param>
/// <param name="ClientId">Optional OIDC client scope — reserved for future realm / client role distinction.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IsSystem">Mark the role as platform-provisioned (prevents CRUD via endpoints).</param>
public sealed record CreateRoleCommand(
    string Name,
    MultiTenancySides MultiTenancySides,
    Guid? TenantId,
    string? ClientId = null,
    string? Description = null,
    bool IsSystem = false);
