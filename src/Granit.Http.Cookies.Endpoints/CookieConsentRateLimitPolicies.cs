namespace Granit.Http.Cookies.Endpoints;

/// <summary>
/// Rate-limiting policy names used by the cookie consent endpoints.
/// Configure the limits under <c>RateLimiting:Policies:{name}</c>.
/// </summary>
public static class CookieConsentRateLimitPolicies
{
    /// <summary>
    /// Policy for the anonymous consent capture endpoint (<c>POST /cookies/consent</c>).
    /// Recommended: FixedWindow per IP — a legitimate user posts a handful of decisions,
    /// never dozens per minute.
    /// </summary>
    public const string Record = "cookie-consent";
}
