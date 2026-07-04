namespace Granit.Http.Cookies;

/// <summary>
/// Cookie consent categories aligned with privacy regulations.
/// </summary>
public enum CookieCategory
{
    /// <summary>Cookies essential for the application to function (no consent required).</summary>
    StrictlyNecessary,

    /// <summary>Cookies that remember user preferences (language, theme).</summary>
    Preferences,

    /// <summary>Cookies used for analytics and usage tracking.</summary>
    Analytics,

    /// <summary>Cookies used for advertising and marketing.</summary>
    Marketing,

    /// <summary>Cookies used for selling or sharing personal information (CCPA "Do Not Sell or Share").</summary>
    SaleOrSharing,
}
