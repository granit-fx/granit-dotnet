namespace Granit.Settings.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Settings EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitSettingsDbProperties
{
    /// <summary>
    /// Table name prefix for all settings tables. Default: <c>"settings_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "settings_";

    /// <summary>
    /// Database schema for all settings tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
