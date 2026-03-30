namespace Granit.Privacy.OptOut;

/// <summary>
/// Constants for the opt-out subsystem.
/// </summary>
public static class OptOutConstants
{
    /// <summary>
    /// Name of the HTTP-Only cookie used to track anonymous opt-out requests (CCPA guest opt-out).
    /// Applications using <c>Granit.Http.Cookies</c> should register this cookie as
    /// <c>StrictlyNecessary</c> in their <c>AddGranitCookies()</c> builder for audit compliance:
    /// <code>
    /// cookies.RegisterCookie(new CookieDefinition(
    ///     OptOutConstants.CookieName, CookieCategory.StrictlyNecessary, 730, true,
    ///     "CCPA anonymous opt-out tracking"));
    /// </code>
    /// </summary>
    public const string CookieName = "granit_optout_id";

    /// <summary>Retention period for the opt-out cookie in days (2 years).</summary>
    public const int RetentionDays = 730;
}
