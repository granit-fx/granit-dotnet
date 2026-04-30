namespace Granit.Entities.Details;

/// <summary>
/// Immutable descriptor for one detail-view variant on an entity (e.g. <c>"default"</c>,
/// <c>"compact"</c>). An entity may declare multiple variants — the renderer picks one
/// via <c>&lt;EntityDetail name=... variant=... /&gt;</c>.
/// </summary>
public sealed record DetailDescriptor
{
    /// <summary>Variant name, unique per entity (e.g. <c>"default"</c>, <c>"compact"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The detail's sections, in declaration order.</summary>
    public required IReadOnlyList<DetailSectionDescriptor> Sections { get; init; }

    /// <summary>The side panels surfaced in the detail's right rail, in declaration order.</summary>
    public required IReadOnlyList<SidePanelDescriptor> SidePanels { get; init; }
}
