namespace Granit.QueryEngine.Meta;

/// <summary>
/// Filter group metadata for frontend auto-configuration.
/// </summary>
/// <param name="Name">Group name.</param>
/// <param name="Label">User-facing label.</param>
/// <param name="Presets">Available presets in this group.</param>
public sealed record FilterGroupMeta(
    string Name,
    string Label,
    IReadOnlyList<PresetMeta> Presets);

/// <summary>
/// Preset metadata within a filter group.
/// </summary>
/// <param name="Name">Preset name.</param>
/// <param name="Label">User-facing label.</param>
/// <param name="IsDefault">Whether this preset is active by default.</param>
public sealed record PresetMeta(
    string Name,
    string Label,
    bool IsDefault);
