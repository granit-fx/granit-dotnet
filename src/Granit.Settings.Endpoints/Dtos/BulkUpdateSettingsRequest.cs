namespace Granit.Settings.Endpoints.Dtos;

/// <summary>
/// Bulk update payload for setting values at a given scope (global or tenant).
/// </summary>
/// <param name="Settings">
/// Entries to apply. Each entry is committed individually — a failure on one entry does not
/// roll back the others. See <see cref="BulkUpdateSettingsResponse"/> for per-entry outcomes.
/// </param>
public sealed record BulkUpdateSettingsRequest(IReadOnlyList<BulkSettingEntry> Settings);

/// <summary>
/// A single entry in a bulk update payload.
/// </summary>
/// <param name="Key">Setting name.</param>
/// <param name="Value">
/// New value, or <see langword="null"/> to clear the override and fall back to the higher-priority
/// provider (e.g. tenant → global, global → default).
/// </param>
public sealed record BulkSettingEntry(string Key, string? Value);
