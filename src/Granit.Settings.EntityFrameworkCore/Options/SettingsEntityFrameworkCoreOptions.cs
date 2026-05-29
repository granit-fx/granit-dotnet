using Microsoft.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for
/// <c>SettingsEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitSettingsEntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// Settings is currently single-scope (one shared host table) — scoping is encoded in
/// <c>SettingRecord.ProviderName</c> + <c>ProviderKey</c> rather than tenant rows. The
/// Options struct mirrors the V1/V2/V3 module shape for API consistency; a future
/// <c>DualScopeStorageMode</c> field can be added without a breaking change should the
/// need arise (RGPD lessivage of per-tenant settings).
/// </remarks>
public sealed class SettingsEntityFrameworkCoreOptions
{
    /// <summary>
    /// EF Core configuration for the <c>SettingsDbContext</c> — provider + connection
    /// string. Required.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }
}
