namespace Granit.Entities.Forms;

/// <summary>
/// Immutable descriptor for one form section, built via
/// <see cref="SectionBuilder{TEntity}"/>.
/// </summary>
public sealed record SectionDescriptor
{
    /// <summary>Stable section key (e.g. <c>"general"</c>, <c>"financial"</c>) — used as the i18n key suffix and the layout-customization addressing key (Phase 2).</summary>
    public required string Key { get; init; }

    /// <summary>i18n key for the section header (resolved client-side).</summary>
    public string? LabelKey { get; init; }

    /// <summary>Display order within the form (lower first).</summary>
    public int Order { get; init; }

    /// <summary>The section's fields, in declaration order.</summary>
    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }

    /// <summary>True when the section is collapsed by default in the renderer.</summary>
    public bool CollapsedByDefault { get; init; }
}
