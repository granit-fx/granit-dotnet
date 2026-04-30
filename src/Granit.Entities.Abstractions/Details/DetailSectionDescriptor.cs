namespace Granit.Entities.Details;

/// <summary>
/// Immutable descriptor for one detail-view section.
/// </summary>
/// <remarks>
/// The detail view supports two source modes (mutually exclusive per section):
/// <list type="bullet">
///   <item>
///     <see cref="InheritsFromFormVariant"/> — the section reuses the structure of a
///     form variant (sections + fields), in read mode by default. Convenient for
///     symmetric "view as I edit" layouts.
///   </item>
///   <item>
///     Free-form — the section's <see cref="Fields"/> are explicitly listed (PascalCase
///     property names of the entity), independent of any form variant.
///   </item>
/// </list>
/// </remarks>
public sealed record DetailSectionDescriptor
{
    /// <summary>Stable section key (e.g. <c>"general"</c>, <c>"financial"</c>).</summary>
    public required string Key { get; init; }

    /// <summary>i18n key for the section header (resolved client-side).</summary>
    public string? LabelKey { get; init; }

    /// <summary>Display order within the detail view (lower first).</summary>
    public int Order { get; init; }

    /// <summary>
    /// When set, the section inherits its sections + fields from the named form variant
    /// (typically <c>"default"</c>). Mutually exclusive with <see cref="Fields"/>.
    /// </summary>
    public string? InheritsFromFormVariant { get; init; }

    /// <summary>
    /// Free-form list of property names to surface in the section. PascalCase names of the
    /// entity. Mutually exclusive with <see cref="InheritsFromFormVariant"/>.
    /// </summary>
    public IReadOnlyList<string>? Fields { get; init; }
}
