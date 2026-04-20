using Granit.Settings.Definitions;

namespace Granit.Settings.Endpoints.Dtos;

/// <summary>
/// Admin-facing view of a setting: metadata merged with the current resolved value.
/// </summary>
/// <param name="Key">Setting name (lookup key).</param>
/// <param name="Label">Display label (from <see cref="SettingDefinition.DisplayName"/>).</param>
/// <param name="Description">Long description (from <see cref="SettingDefinition.Description"/>).</param>
/// <param name="DefaultValue">Default value when no provider supplies one.</param>
/// <param name="Value">Current resolved value at this scope. <c>"***"</c> when <see cref="IsEncrypted"/>.</param>
/// <param name="ValueKind">Shape of the value (<see cref="Granit.Settings.Definitions.ValueKind"/>).</param>
/// <param name="AllowedValues">Optional closed allow-list; <c>null</c> when no constraint.</param>
/// <param name="IsEncrypted">When <c>true</c>, the raw value is masked and cannot be read back.</param>
public sealed record AdminAppSettingResponse(
    string Key,
    string? Label,
    string? Description,
    string? DefaultValue,
    string? Value,
    ValueKind ValueKind,
    IReadOnlyList<string>? AllowedValues,
    bool IsEncrypted);
