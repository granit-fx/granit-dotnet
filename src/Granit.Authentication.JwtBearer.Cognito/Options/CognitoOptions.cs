namespace Granit.Authentication.JwtBearer.Cognito.Options;

/// <summary>
/// Configuration options for AWS Cognito OIDC authentication.
/// </summary>
public sealed class CognitoOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Cognito";

    /// <summary>
    /// OIDC authority URL (e.g. <c>https://cognito-idp.eu-west-1.amazonaws.com/eu-west-1_XXXXXXXXX</c>).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Cognito App Client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Require HTTPS for OIDC metadata. Default: true in production.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Expected audience in the token. Defaults to <see cref="ClientId"/>.</summary>
    public string? Audience { get; set; }

}
