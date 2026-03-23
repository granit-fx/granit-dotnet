namespace Granit.Authentication.TokenManagement.Options;

/// <summary>
/// Global options for OAuth 2.0 token lifecycle management.
/// </summary>
public sealed class TokenManagementOptions
{
    /// <summary>
    /// The configuration section name used by <c>IConfiguration.GetSection()</c>.
    /// </summary>
    public const string SectionName = "TokenManagement";

    /// <summary>
    /// The default margin subtracted from token lifetimes before caching,
    /// ensuring tokens are refreshed before they expire. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DefaultCacheMargin { get; set; } = TimeSpan.FromSeconds(30);
}
