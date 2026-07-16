using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.Local.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the local-identity EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Set these properties at application startup, before <c>ConfigureServices</c> completes. EF Core
/// caches the compiled model after first use — later mutations have no effect.
/// </para>
/// <para>
/// The prefix defaults to <c>"openiddict_"</c> so the model is byte-identical to the pre-split
/// consolidated schema — hosts need no data migration. Renaming to <c>"identity_"</c> is a separate,
/// opt-in decision.
/// </para>
/// </remarks>
public static class GranitIdentityLocalDbProperties
{
    /// <summary>Table name prefix for the local-identity tables. Default: <c>"openiddict_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "openiddict_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for host-level tables. Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>,
    /// then <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
