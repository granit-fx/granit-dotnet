using System.Diagnostics;

namespace Granit.OpenIddict.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.OpenIddict distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class OpenIddictActivitySource
{
    /// <summary>The name of the Granit.OpenIddict <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.OpenIddict";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

#pragma warning disable GRSEC003 // Operation name constants, not secrets
    internal const string TokenIssuance = "openiddict.token-issuance";
    internal const string TokenRevocation = "openiddict.token-revocation";
    internal const string UserRegistration = "openiddict.user-registration";
    internal const string UserAuthentication = "openiddict.user-authentication";
    internal const string TwoFactorChallenge = "openiddict.two-factor-challenge";
    internal const string Impersonation = "openiddict.impersonation";
    internal const string PasswordReset = "openiddict.password-reset";
    internal const string AccountDeletion = "openiddict.account-deletion";
#pragma warning restore GRSEC003

    // ──── Tag names ────

    internal const string TagGrantType = "openiddict.grant_type";
    internal const string TagClientId = "openiddict.client_id";
    internal const string TagProvider = "openiddict.provider";
}
