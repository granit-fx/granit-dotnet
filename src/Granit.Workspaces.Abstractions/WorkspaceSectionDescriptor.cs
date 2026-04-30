namespace Granit.Workspaces;

/// <summary>
/// Immutable descriptor for one section of a workspace. Sections are the
/// collapsible groups in the navigation tree (e.g. <c>"sales"</c>, <c>"reports"</c>).
/// </summary>
/// <param name="Key">Stable section key, unique within the parent workspace.</param>
/// <param name="DisplayKey">i18n key for the section header, or <see langword="null"/>.</param>
/// <param name="Order">Display order within the workspace.</param>
/// <param name="CollapsedByDefault">Whether the section starts collapsed.</param>
/// <param name="Items">Items in declaration order.</param>
public sealed record WorkspaceSectionDescriptor(
    string Key,
    string? DisplayKey,
    int Order,
    bool CollapsedByDefault,
    IReadOnlyList<WorkspaceItemDescriptor> Items);
