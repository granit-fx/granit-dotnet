namespace Granit.Identity.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Identity EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitIdentityDbProperties
{
    /// <summary>
    /// Table name prefix for all identity tables. Default: <c>"identity_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "identity_";

    /// <summary>
    /// Database schema for all identity tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
