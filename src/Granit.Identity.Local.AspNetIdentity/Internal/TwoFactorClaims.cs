namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Claim types used to track per-factor two-factor enrollment that ASP.NET Core Identity
/// does not model natively. Stored as user claims so no schema change (and therefore no
/// app-side migration) is required.
/// </summary>
internal static class TwoFactorClaims
{
    /// <summary>Present with value <c>"true"</c> when the user has enrolled the email OTP factor.</summary>
    internal const string EmailOtpEnabled = "granit:2fa:email_otp";

    /// <summary>The value stored for an enabled factor claim.</summary>
    internal const string EnabledValue = "true";
}
