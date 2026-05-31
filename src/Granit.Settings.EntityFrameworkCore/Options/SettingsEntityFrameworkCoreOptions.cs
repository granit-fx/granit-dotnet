using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for
/// <c>SettingsEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitSettingsEntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// Settings is single-scope (one shared host table) — scoping is encoded in
/// <c>SettingRecord.ProviderName</c> + <c>ProviderKey</c> rather than tenant rows.
/// </remarks>
public sealed class SettingsEntityFrameworkCoreOptions
{
    /// <summary>
    /// EF Core configuration for the <c>SettingsDbContext</c> — provider + connection
    /// string. Required.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }
}
