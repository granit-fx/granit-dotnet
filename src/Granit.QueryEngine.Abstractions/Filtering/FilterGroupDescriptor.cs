namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about a mutually exclusive filter preset group.
/// Presets within a group use OR semantics; groups are combined with AND.
/// </summary>
public sealed record FilterGroupDescriptor
{
    /// <summary>Unique name of this group (e.g. <c>"Status"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>User-facing label, or <c>null</c> to use the name.</summary>
    public string? Label { get; init; }

    /// <summary>The presets within this group.</summary>
    public required IReadOnlyList<PresetDescriptor> Presets { get; init; }
}
