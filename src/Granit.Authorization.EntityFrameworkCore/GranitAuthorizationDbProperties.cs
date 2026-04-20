using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Authorization.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Authorization EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Authorization grants are host-level: they may target host users (<c>TenantId == null</c>)
/// or tenant-scoped users (<c>TenantId</c> set), but the storage itself always lives in the
/// host schema alongside Identity and OpenIddict.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitAuthorizationDbProperties
{
    /// <summary>
    /// Table name prefix for all authorization tables. Default: <c>"authorization_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "authorization_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for authorization tables. Host-level storage:
    /// falls back to <see cref="GranitDbDefaults.HostDbSchema"/> when not explicitly set,
    /// then to <see cref="GranitDbDefaults.DbSchema"/> as a final fallback for
    /// shared-database deployments.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
