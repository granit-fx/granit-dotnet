namespace Granit.Settings.Values;

/// <summary>
/// Represents the value of a setting for a given provider and key.
/// </summary>
/// <param name="Name">Setting name.</param>
/// <param name="ProviderName">Provider name (e.g. "G", "T", "U", "C", "D").</param>
/// <param name="ProviderKey">Provider key (null = Global, tenantId = Tenant, userId = User).</param>
/// <param name="Value">Setting value (plain text — encryption is handled by ISettingStoreWriter).</param>
public sealed record SettingValue(
    string Name,
    string ProviderName,
    string? ProviderKey,
    string? Value);
