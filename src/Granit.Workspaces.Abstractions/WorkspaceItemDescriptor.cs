namespace Granit.Workspaces;

/// <summary>
/// Closed enum of item kinds the workspace tree exposes.
/// </summary>
public enum WorkspaceItemKind
{
    /// <summary>Reference to an <c>EntityDefinition</c> — the renderer mounts the entity's list / kanban view at this slot.</summary>
    Entity,

    /// <summary>Reference to a <c>DashboardDefinition</c>.</summary>
    Dashboard,

    /// <summary>Plain hyperlink — internal SPA route or external URL.</summary>
    Link,

    /// <summary>Sub-workspace — recurses into a child <see cref="WorkspaceDescriptor"/>.</summary>
    SubWorkspace,
}

/// <summary>
/// Immutable descriptor for one item shown in a workspace section.
/// </summary>
/// <param name="Kind">The kind of slot.</param>
/// <param name="Order">Display order within the section.</param>
/// <param name="DisplayKey">i18n key for the item label, or <see langword="null"/> when the renderer should fall back to the referenced primitive's own label.</param>
/// <param name="Icon">Icon override for this slot, or <see langword="null"/>.</param>
/// <param name="EntityName">Wire identifier of the referenced <c>EntityDefinition</c> when <see cref="Kind"/> is <see cref="WorkspaceItemKind.Entity"/>; <see langword="null"/> otherwise.</param>
/// <param name="EntityViewName">Optional preferred default <c>EntityView</c> name on the entity (e.g. <c>"open"</c>).</param>
/// <param name="EntityPresetOverlay">Optional additive preset overlay (per ADR-048 §4) — extra filters, sort, columns layered on top of the entity's compiled defaults. Server-side validation enforces additive-only semantics.</param>
/// <param name="DashboardName">Wire identifier of the referenced <c>DashboardDefinition</c> when <see cref="Kind"/> is <see cref="WorkspaceItemKind.Dashboard"/>.</param>
/// <param name="LinkUrl">Internal route or absolute URL when <see cref="Kind"/> is <see cref="WorkspaceItemKind.Link"/>.</param>
/// <param name="SubWorkspaceName">Wire identifier of the referenced <see cref="WorkspaceDescriptor"/> when <see cref="Kind"/> is <see cref="WorkspaceItemKind.SubWorkspace"/>.</param>
/// <param name="RequiresPermission">Optional permission gate — drops the item from the manifest payload when the user does not hold the permission. Defense-in-depth (ADR-040).</param>
public sealed record WorkspaceItemDescriptor(
    WorkspaceItemKind Kind,
    int Order,
    string? DisplayKey,
    string? Icon,
    string? EntityName,
    string? EntityViewName,
    IReadOnlyDictionary<string, object?>? EntityPresetOverlay,
    string? DashboardName,
    string? LinkUrl,
    string? SubWorkspaceName,
    string? RequiresPermission);
