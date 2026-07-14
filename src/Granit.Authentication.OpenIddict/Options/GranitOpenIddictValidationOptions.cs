namespace Granit.Authentication.OpenIddict.Options;

/// <summary>
/// Configuration options for OpenIddict token validation on resource servers.
/// </summary>
public sealed class GranitOpenIddictValidationOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Authentication:OpenIddict";

    /// <summary>
    /// Gets or sets the OIDC issuer URI of the remote authorization server.
    /// </summary>
    public Uri? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the expected audience (resource server identifier).
    /// </summary>
    public string? Audience { get; set; }
}
