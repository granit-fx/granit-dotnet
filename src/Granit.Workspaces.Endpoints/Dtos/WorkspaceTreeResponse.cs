namespace Granit.Workspaces.Endpoints.Dtos;

/// <summary>
/// Discovery payload returned by <c>GET /api/workspaces</c>. Carries a schema
/// version so the renderer can adapt to additive changes without a full
/// migration.
/// </summary>
/// <param name="SchemaVersion">Workspace tree schema version (semver-major). Bumps on breaking shape changes.</param>
/// <param name="Workspaces">Workspaces visible to the requesting user, sorted by Order then Name.</param>
public sealed record WorkspaceTreeResponse(
    int SchemaVersion,
    IReadOnlyList<WorkspaceResponse> Workspaces);

/// <summary>One workspace in the tree.</summary>
/// <param name="Name">Wire identifier.</param>
/// <param name="DisplayKey">i18n key for the user-facing display name.</param>
/// <param name="Icon">Icon name from the catalog.</param>
/// <param name="Order">Display order in the global workspace list.</param>
/// <param name="IsShell">Whether this workspace is a Framework shell — populated by contributions.</param>
/// <param name="Sections">Sections the user can see (each carrying at least one item).</param>
public sealed record WorkspaceResponse(
    string Name,
    string? DisplayKey,
    string? Icon,
    int Order,
    bool IsShell,
    IReadOnlyList<WorkspaceSectionResponse> Sections);

/// <summary>One section within a workspace.</summary>
/// <param name="Key">Stable section key.</param>
/// <param name="DisplayKey">i18n key for the section header.</param>
/// <param name="Order">Display order within the workspace.</param>
/// <param name="CollapsedByDefault">Whether the section starts collapsed.</param>
/// <param name="Items">Items the user can see (already permission-filtered).</param>
public sealed record WorkspaceSectionResponse(
    string Key,
    string? DisplayKey,
    int Order,
    bool CollapsedByDefault,
    IReadOnlyList<WorkspaceItemResponse> Items);

/// <summary>One item in a workspace section.</summary>
/// <param name="Kind">Slot kind (<see cref="WorkspaceItemKind"/>).</param>
/// <param name="Order">Display order within the section.</param>
/// <param name="DisplayKey">i18n key for the item label.</param>
/// <param name="Icon">Icon override.</param>
/// <param name="EntityName">Wire identifier of the referenced EntityDefinition (Entity items only).</param>
/// <param name="EntityViewName">Preferred default EntityView name for the entity.</param>
/// <param name="EntityPresetOverlay">Additive preset overlay for the entity.</param>
/// <param name="DashboardName">Wire identifier of the referenced DashboardDefinition (Dashboard items only).</param>
/// <param name="LinkUrl">Internal route or absolute URL (Link items only).</param>
/// <param name="SubWorkspaceName">Wire identifier of the referenced sub-workspace (SubWorkspace items only).</param>
public sealed record WorkspaceItemResponse(
    WorkspaceItemKind Kind,
    int Order,
    string? DisplayKey,
    string? Icon,
    string? EntityName,
    string? EntityViewName,
    IReadOnlyDictionary<string, object?>? EntityPresetOverlay,
    string? DashboardName,
    string? LinkUrl,
    string? SubWorkspaceName);
