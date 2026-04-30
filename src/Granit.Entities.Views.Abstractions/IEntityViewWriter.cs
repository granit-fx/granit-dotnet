using System.Text.Json.Nodes;

namespace Granit.Entities.Views;

/// <summary>
/// Write-side service for the EntityView aggregate. Every operation here is
/// audited via <c>Granit.Auditing</c>; the implementation enforces the closed
/// permission set of <see cref="EntityViewPermissions"/>.
/// </summary>
public interface IEntityViewWriter
{
    /// <summary>
    /// Create a new Personal view for the current user. Requires
    /// <see cref="EntityViewPermissions.Create"/>.
    /// </summary>
    Task<EntityViewDescriptor> CreateAsync(
        EntityViewCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing view (name, description, icon, state). Requires ownership
    /// (Personal / Shared) or <see cref="EntityViewPermissions.Manage"/> (Tenant).
    /// <c>BasedOn</c> and <c>Kind</c> are immutable per ADR-047 §3 — any attempt to
    /// change them is rejected.
    /// </summary>
    Task<EntityViewDescriptor> UpdateAsync(
        Guid id,
        EntityViewUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Delete a view. Owner can delete their own; <see cref="EntityViewPermissions.DeleteAny"/> for moderation.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toggle the admin-pinned flag (workspace tab strip). Requires <see cref="EntityViewPermissions.Manage"/>.</summary>
    Task<EntityViewDescriptor> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken = default);

    /// <summary>Toggle the tenant-default flag. Requires <see cref="EntityViewPermissions.Manage"/>.</summary>
    Task<EntityViewDescriptor> SetTenantDefaultAsync(Guid id, bool isDefault, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle the personal-default flag for the current user. Each user may have at
    /// most one <c>IsPersonalDefault</c> view per entity; setting one clears any
    /// other. Available to any authenticated user.
    /// </summary>
    Task<EntityViewDescriptor> SetPersonalDefaultAsync(Guid id, bool isPersonalDefault, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promote a Personal view to Shared. Requires <see cref="EntityViewPermissions.Share"/>.
    /// </summary>
    Task<EntityViewDescriptor> ShareAsync(
        Guid id,
        EntityViewSharedWith audience,
        CancellationToken cancellationToken = default);
}

/// <summary>Request payload for <see cref="IEntityViewWriter.CreateAsync"/>.</summary>
/// <param name="EntityName">The entity's wire identifier.</param>
/// <param name="BasedOn">Name of the compiled collection the view deltas over.</param>
/// <param name="Kind">View kind inherited from <paramref name="BasedOn"/>.</param>
/// <param name="Name">User-facing label.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Icon">Optional icon name.</param>
/// <param name="State">JSONB delta payload — validated against the schema attached to <paramref name="Kind"/>.</param>
public sealed record EntityViewCreateRequest(
    string EntityName,
    string BasedOn,
    string Kind,
    string Name,
    string? Description,
    string? Icon,
    JsonObject State);

/// <summary>Request payload for <see cref="IEntityViewWriter.UpdateAsync"/>. Cannot mutate <c>BasedOn</c> or <c>Kind</c>.</summary>
/// <param name="Name">User-facing label.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Icon">Optional icon name.</param>
/// <param name="State">JSONB delta payload — validated against the schema attached to the view's existing <c>Kind</c>.</param>
public sealed record EntityViewUpdateRequest(
    string Name,
    string? Description,
    string? Icon,
    JsonObject State);
