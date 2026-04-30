namespace Granit.Entities.Forms;

/// <summary>
/// Immutable descriptor for one form variant on an entity (e.g. <c>"default"</c>,
/// <c>"quick"</c>, <c>"wizard"</c>). An entity may declare multiple variants — the
/// renderer picks one via <c>&lt;EntityForm name=... variant=... /&gt;</c>.
/// </summary>
public sealed record FormDescriptor
{
    /// <summary>Variant name, unique per entity (e.g. <c>"default"</c>, <c>"quick"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The form's sections, in declaration order.</summary>
    public required IReadOnlyList<SectionDescriptor> Sections { get; init; }

    /// <summary>
    /// When <see langword="true"/>, tenant admins (Tier B Layer 1, ADR-040) may reorder /
    /// regroup / hide compiled fields in this form via the customization endpoint. They
    /// can NEVER add new fields, change validation, or change widget — see ADR-040 §Tier B.
    /// </summary>
    public bool Customizable { get; init; }
}
