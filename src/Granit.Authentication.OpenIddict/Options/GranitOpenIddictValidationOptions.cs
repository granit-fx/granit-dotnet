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

    /// <summary>
    /// Gets or sets whether DPoP proof-of-possession (RFC 9449) is required for all requests.
    /// When <see langword="true"/>, requests using <c>Authorization: Bearer</c> without a
    /// <c>DPoP</c> proof header are rejected with 401.
    /// Required for FAPI 2.0 §5.3.4 compliance.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool RequireDPoP { get; set; }
}
