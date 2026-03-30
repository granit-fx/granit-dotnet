using System.Diagnostics;

namespace Granit.Identity.Local.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Identity.Local distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class IdentityLocalActivitySource
{
    /// <summary>The name of the Granit.Identity.Local <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Identity.Local";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

#pragma warning disable GRSEC003 // Operation name constants, not secrets
    internal const string UserRegistration = "identity-local.user-registration";
    internal const string UserAuthentication = "identity-local.user-authentication";
    internal const string TwoFactorChallenge = "identity-local.two-factor-challenge";
    internal const string Impersonation = "identity-local.impersonation";
    internal const string PasswordReset = "identity-local.password-reset";
    internal const string AccountDeletion = "identity-local.account-deletion";
#pragma warning restore GRSEC003

    // ──── Tag names ────

    internal const string TagProvider = "identity_local.provider";
}
