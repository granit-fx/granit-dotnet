namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response exposing the current Identity.Local module configuration for the anonymous login screen.
/// </summary>
/// <param name="AllowSelfRegistration">Whether self-registration is enabled.</param>
/// <param name="ExternalProviders">
/// The external login providers available for sign-in (configured AND backed by a registered
/// authentication handler). Empty when none are configured or no auth-server is present.
/// </param>
public sealed record IdentityLocalConfigResponse(
    bool AllowSelfRegistration,
    IReadOnlyList<ExternalLoginProviderInfo> ExternalProviders);

/// <summary>
/// An external login provider offered on the anonymous login screen.
/// </summary>
/// <param name="Name">The authentication scheme name — passed to <c>POST /account/external-logins/challenge/{provider}</c>.</param>
/// <param name="Type">The provider kind (Google, Microsoft, Apple, GitHub, Facebook, Oidc).</param>
/// <param name="DisplayName">A human-friendly label for the "Continue with …" button.</param>
public sealed record ExternalLoginProviderInfo(string Name, string Type, string DisplayName);
