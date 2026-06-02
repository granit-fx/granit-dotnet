namespace Granit.Http.Cookies.CookieConsent.Options;

/// <summary>
/// Configuration options for the @cookieconsent/core CMP integration.
/// Bound to the <c>Http:Cookies:CookieConsent</c> configuration section.
/// </summary>
/// <remarks>
/// Category names must match those configured in the front-end
/// <c>@granit/cookies-cookieconsent</c> adapter. The defaults align with
/// the <c>@cookieconsent/core</c> built-in category identifiers.
/// </remarks>
public sealed class CookieConsentOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Http:Cookies:CookieConsent";

    /// <summary>
    /// Name of the cookie where @cookieconsent/core stores consent decisions.
    /// Default: <c>"cc_cookie"</c>.
    /// </summary>
    public string CookieName { get; set; } = "cc_cookie";

    /// <summary>
    /// Category name used by the front-end for <see cref="CookieCategory.Preferences"/>.
    /// Default: <c>"functional"</c>.
    /// </summary>
    public string FunctionalCategoryName { get; set; } = "functional";

    /// <summary>
    /// Category name used by the front-end for <see cref="CookieCategory.Analytics"/>.
    /// Default: <c>"analytics"</c>.
    /// </summary>
    public string AnalyticsCategoryName { get; set; } = "analytics";

    /// <summary>
    /// Category name used by the front-end for <see cref="CookieCategory.Marketing"/>.
    /// Default: <c>"marketing"</c>.
    /// </summary>
    public string MarketingCategoryName { get; set; } = "marketing";

    /// <summary>
    /// Category name used by the front-end for <see cref="CookieCategory.SaleOrSharing"/>.
    /// Default: <c>"sale_or_sharing"</c>.
    /// </summary>
    public string SaleOrSharingCategoryName { get; set; } = "sale_or_sharing";
}
