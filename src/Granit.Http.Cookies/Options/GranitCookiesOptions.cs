namespace Granit.Http.Cookies.Options;

/// <summary>
/// Configuration options for the Granit.Http.Cookies module.
/// </summary>
public sealed class GranitCookiesOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Http:Cookies";

    /// <summary>
    /// When <c>true</c>, writing an unregistered cookie throws <see cref="Exceptions.UnregisteredCookieException"/>.
    /// Default: <c>true</c> (Fail-Fast).
    /// </summary>
    public bool ThrowOnUnregistered { get; set; } = true;

    /// <summary>
    /// Default retention period in days for cookies that do not specify one.
    /// Default: <c>30</c>. Must be within <c>[1, 395]</c>; the upper bound
    /// matches the CNIL 13-month hard cap (GDPR Art. 5(1)(e) — storage
    /// limitation). Cookies that legitimately need a longer lifetime should
    /// declare an explicit <see cref="CookieDefinition.RetentionDays"/>.
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 30;

    /// <summary>
    /// Upper bound on any cookie's retention (days). CNIL caps analytics/
    /// marketing cookies at 13 months; the 395-day value includes leap-year
    /// slack.
    /// </summary>
    public const int MaxRetentionDays = 395;

    /// <summary>
    /// Third-party services that set cookies on the client (analytics, marketing, etc.).
    /// Declared in configuration and exposed to the front-end for CMP setup.
    /// </summary>
    /// <example>
    /// <code>
    /// "Cookies": {
    ///   "ThirdPartyServices": [
    ///     { "Name": "matomo", "Category": "Analytics", "CookiePatterns": ["^_pk_"] },
    ///     { "Name": "hubspot", "Category": "Marketing", "CookiePatterns": ["^__hs"] }
    ///   ]
    /// }
    /// </code>
    /// </example>
    public List<ThirdPartyServiceOptions> ThirdPartyServices { get; set; } = [];
}

/// <summary>
/// Configuration entry for a third-party service.
/// </summary>
public sealed class ThirdPartyServiceOptions
{
    /// <summary>Unique service identifier (e.g. "matomo", "hubspot").</summary>
    public required string Name { get; set; }

    /// <summary>GDPR consent category.</summary>
    public required CookieCategory Category { get; set; }

    /// <summary>
    /// Regex patterns matching cookies set by this service.
    /// Used by CMPs to clean up cookies when consent is revoked.
    /// </summary>
    public List<string> CookiePatterns { get; set; } = [];
}
