namespace Granit.Entities.Layouts;

/// <summary>
/// Base record for an entity-list layout declaration. Concrete shapes (kanban,
/// future calendar / map / gallery) carry the kind-specific configuration
/// alongside this base.
/// </summary>
public abstract record EntityListLayoutDescriptor
{
    /// <summary>Layout kind — drives front-end component selection.</summary>
    public required EntityListLayoutKind Kind { get; init; }

    /// <summary>
    /// When <see langword="true"/>, this layout is the one the
    /// <c>EntityListViewSwitcher</c> picks on first render. At most one layout
    /// per entity may set it; framework default falls back to
    /// <see cref="EntityListLayoutKind.List"/> when no layout opts in.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// When set, the layout is dropped from the manifest payload entirely if
    /// the user lacks this permission — defense in depth (per ADR-040 §6).
    /// </summary>
    public string? RequiresPermission { get; init; }
}
