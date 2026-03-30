namespace Granit.Http.Cookies;

/// <summary>
/// Cookie consent categories aligned with privacy regulations.
/// </summary>
public enum CookieCategory
{
    /// <summary>Cookies essential for the application to function (no consent required).</summary>
    StrictlyNecessary = 0,

    /// <summary>Cookies that remember user preferences (language, theme).</summary>
    Preferences = 1,

    /// <summary>Cookies used for analytics and usage tracking.</summary>
    Analytics = 2,

    /// <summary>Cookies used for advertising and marketing.</summary>
    Marketing = 3,

    /// <summary>Cookies used for selling or sharing personal information (CCPA "Do Not Sell or Share").</summary>
    SaleOrSharing = 4,
}
