namespace Granit.Http.Cookies;

/// <summary>
/// Describes the consent model applicable for cookie handling.
/// Mirrors <c>Granit.Privacy.Regulations.ConsentModel</c> without creating a dependency.
/// </summary>
public enum CookieConsentMode
{
    /// <summary>User must explicitly opt in before non-essential cookies are set (GDPR, LGPD).</summary>
    OptIn = 0,

    /// <summary>Cookies allowed by default; user can opt out (CCPA).</summary>
    OptOut = 1,

    /// <summary>Opt-in for sensitive categories, opt-out for non-sensitive (some US states).</summary>
    Hybrid = 2,

    /// <summary>No specific consent requirement for this jurisdiction.</summary>
    None = 3,
}
