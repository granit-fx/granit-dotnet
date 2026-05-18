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
/// <param name="DynamicInclude">
/// Optional dynamic-inclusion spec (per ADR-057 §3) — when set, the composer
/// synthesises one <see cref="WorkspaceItemKind.SubWorkspace"/> item per
/// registered workspace whose name satisfies the spec's predicate, lifting
/// the matched workspace's icon / display key / order. <see langword="null"/>
/// after composition resolves the spec (the descriptor exposed at runtime
/// never carries an unresolved spec).
/// </param>
public sealed record WorkspaceSectionDescriptor(
    string Key,
    string? DisplayKey,
    int Order,
    bool CollapsedByDefault,
    IReadOnlyList<WorkspaceItemDescriptor> Items,
    WorkspaceIncludeSpec? DynamicInclude = null);

/// <summary>
/// Declarative spec used by
/// <see cref="WorkspaceSectionBuilder.IncludeWorkspacesMatching(Predicate{string})"/>
/// to ask the composer to absorb every registered workspace satisfying
/// <see cref="NamePredicate"/> as a <see cref="WorkspaceItemKind.SubWorkspace"/>
/// item (per ADR-057 §3). The matched workspaces contribute their own icon /
/// display key / order — no duplication between the including section and the
/// included shell.
/// </summary>
public sealed record WorkspaceIncludeSpec(Predicate<string> NamePredicate);
