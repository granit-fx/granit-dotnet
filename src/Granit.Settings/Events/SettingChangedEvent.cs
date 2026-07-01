namespace Granit.Settings.Events;

/// <summary>
/// Raised when a setting value is created, updated, or deleted.
/// </summary>
/// <remarks>
/// Published by <see cref="Services.SettingWriter"/> after every write operation.
/// Consumed by audit log handlers for ISO 27001 A.12.4 configuration change logging.
/// </remarks>
/// <param name="SettingName">The setting name (e.g. <c>"App.Theme"</c>).</param>
/// <param name="ProviderName">Scope provider: <c>"G"</c> (global), <c>"T"</c> (tenant), <c>"U"</c> (user).</param>
/// <param name="ProviderKey">Scope key: <c>null</c> for global, tenant ID, or user ID.</param>
/// <param name="OldValue">Previous value (<c>null</c> if the setting was created).</param>
/// <param name="NewValue">New value (<c>null</c> if the setting was deleted).</param>
/// <param name="Timestamp">UTC timestamp of the change.</param>
public sealed record SettingChangedEvent(
    string SettingName,
    string ProviderName,
    string? ProviderKey,
    string? OldValue,
    string? NewValue,
    DateTimeOffset Timestamp);
