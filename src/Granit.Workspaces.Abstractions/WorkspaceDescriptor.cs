namespace Granit.Workspaces;

/// <summary>
/// Immutable descriptor of one workspace, built by a
/// <see cref="WorkspaceDefinition"/> via the fluent
/// <see cref="WorkspaceBuilder"/>. Mirrors <c>EntityDefinitionDescriptor</c>.
/// </summary>
public sealed record WorkspaceDescriptor
{
    /// <summary>Wire identifier (e.g. <c>"Granit.Framework"</c>, <c>"Showcase.Crm"</c>). MUST be unique.</summary>
    public required string Name { get; init; }

    /// <summary>i18n key for the user-facing display name.</summary>
    public string? DisplayKey { get; init; }

    /// <summary>Optional icon name from the catalog.</summary>
    public string? Icon { get; init; }

    /// <summary>Display order in the global workspace list.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Permission required to enter this workspace. When <see langword="null"/>,
    /// the workspace is implicitly visible to every authenticated user. Defense-in-depth
    /// at the per-item level still applies via <see cref="WorkspaceItemDescriptor.RequiresPermission"/>.
    /// </summary>
    public string? RequiresPermission { get; init; }

    /// <summary>
    /// Sections declared on this workspace. Authoritative order is the declaration order;
    /// per-section ordering uses <see cref="WorkspaceSectionDescriptor.Order"/>.
    /// </summary>
    public required IReadOnlyList<WorkspaceSectionDescriptor> Sections { get; init; }

    /// <summary>
    /// True when the workspace is a "shell" — declared by name only, intended to be
    /// populated by <see cref="IFeatureProvider"/> implementations. Empty shells
    /// are auto-filtered from the final tree (ADR-040 §7).
    /// </summary>
    public bool IsShell { get; init; }
}

/// <summary>
/// Surface every <see cref="WorkspaceDefinition"/> exposes to the runtime through DI.
/// </summary>
public interface IWorkspaceDescriptor
{
    /// <summary>Wire identifier of the workspace.</summary>
    string Name { get; }

    /// <summary>Immutable descriptor — built lazily on first access.</summary>
    WorkspaceDescriptor Descriptor { get; }
}
