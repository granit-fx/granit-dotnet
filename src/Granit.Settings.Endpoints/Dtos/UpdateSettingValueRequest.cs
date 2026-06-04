namespace Granit.Settings.Endpoints.Dtos;

/// <summary>
/// Request to update a setting value.
/// </summary>
/// <param name="Value">The new value, or <c>null</c> to clear.</param>
public sealed record UpdateSettingValueRequest(string? Value = null);
