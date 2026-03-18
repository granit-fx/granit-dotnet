namespace Granit.Authorization.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Authorization EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitAuthorizationDbProperties
{
    /// <summary>
    /// Table name prefix for all authorization tables. Default: <c>"auth_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "auth_";

    /// <summary>
    /// Database schema for all authorization tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
