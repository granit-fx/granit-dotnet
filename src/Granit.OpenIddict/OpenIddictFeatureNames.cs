namespace Granit.OpenIddict;

/// <summary>
/// Well-known feature flag names for the OpenIddict module.
/// </summary>
/// <remarks>
/// These features are registered via <c>OpenIddictFeatureDefinitionProvider</c> (auto-discovered
/// by <c>GranitFeaturesModule</c>). Resolution order: Tenant → Plan → Default.
/// </remarks>
public static class OpenIddictFeatureNames
{
    /// <summary>
    /// Enables TOTP-based two-factor authentication. Default: <see langword="true"/>.
    /// </summary>
    public const string TwoFactor = "OpenIddict.TwoFactor";

    /// <summary>
    /// Enables the OAuth 2.0 Device Authorization Grant (RFC 8628). Default: <see langword="false"/>.
    /// </summary>
    public const string DeviceFlow = "OpenIddict.DeviceFlow";

    /// <summary>
    /// Enables external login providers (Google, Microsoft, GitHub). Default: <see langword="true"/>.
    /// </summary>
    public const string ExternalLogins = "OpenIddict.ExternalLogins";

    /// <summary>
    /// Requires PKCE for all authorization code flows. Default: <see langword="true"/>.
    /// </summary>
    public const string PkceRequired = "OpenIddict.PkceRequired";

    /// <summary>
    /// Enables passwordless authentication via WebAuthn/FIDO2 passkeys. Default: <see langword="false"/>.
    /// </summary>
    public const string Passkeys = "OpenIddict.Passkeys";
}
