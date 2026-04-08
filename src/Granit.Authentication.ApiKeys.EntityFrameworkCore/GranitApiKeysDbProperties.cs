using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the ApiKeys EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Authentication.ApiKeys.EntityFrameworkCore</c>. Both the internal
/// <c>AuthenticationApiKeysDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitApiKeysDbProperties
{
    /// <summary>
    /// Table name prefix for all API key tables. Default: <c>"api_keys_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "api_keys_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
