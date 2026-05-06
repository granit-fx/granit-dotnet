using Granit.Entities.Details;

namespace Granit.Entities.Manifests;

/// <summary>One detail-view variant exposed in the manifest.</summary>
/// <param name="Name">Variant name, unique per entity (e.g. <c>"default"</c>).</param>
/// <param name="Sections">Sections in declaration order.</param>
/// <param name="SidePanels">Side-panel slots in the right rail (already permission-filtered).</param>
public sealed record EntityDetailManifest(
    string Name,
    IReadOnlyList<EntityDetailSectionManifest> Sections,
    IReadOnlyList<EntityDetailSidePanelManifest> SidePanels);

/// <summary>One detail-view section.</summary>
/// <param name="Key">Stable section key.</param>
/// <param name="LabelKey">i18n key for the section header.</param>
/// <param name="Order">Display order (lower first).</param>
/// <param name="InheritsFromFormVariant">When set, the section reuses a form variant's structure in read mode. Mutually exclusive with <see cref="Fields"/>.</param>
/// <param name="Fields">Free-form list of property names — used when the section doesn't inherit from a form variant.</param>
public sealed record EntityDetailSectionManifest(
    string Key,
    string? LabelKey,
    int Order,
    string? InheritsFromFormVariant,
    IReadOnlyList<string>? Fields);

/// <summary>One side panel in the detail's right rail.</summary>
/// <param name="Kind">Standard side-panel kind (<see cref="SidePanelKind"/>).</param>
/// <param name="Order">Display order within the rail.</param>
public sealed record EntityDetailSidePanelManifest(
    SidePanelKind Kind,
    int Order);
