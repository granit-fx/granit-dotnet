namespace Granit.OpenIddict.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the OpenIddict EF Core module.
/// </summary>
/// <remarks>
/// Set these properties at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first use — later mutations have no effect.
/// </remarks>
public static class GranitOpenIddictDbProperties
{
    /// <summary>
    /// Table name prefix for all OpenIddict and Identity tables. Default: <c>"oidc_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "oidc_";

    /// <summary>
    /// Database schema for all OpenIddict and Identity tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
