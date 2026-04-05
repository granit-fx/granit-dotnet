namespace Granit.MultiTenancy.EntityFrameworkCore;

/// <summary>
/// Configurable table naming for the multi-tenancy module.
/// Must be set before <c>ConfigureServices</c> completes (EF Core caches the model).
/// </summary>
public static class MultiTenancyDbProperties
{
    /// <summary>
    /// Prefix for all multi-tenancy tables. Default: <c>"tenants_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "tenants_";

    /// <summary>
    /// Optional database schema. Default: <c>null</c> (provider default).
    /// </summary>
    public static string? DbSchema { get; set; }
}
