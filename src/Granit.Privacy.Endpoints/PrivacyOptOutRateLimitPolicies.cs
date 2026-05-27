namespace Granit.Privacy.Endpoints;

/// <summary>
/// Well-known rate-limit policy names attached to privacy opt-out endpoints.
/// </summary>
/// <remarks>
/// <para>
/// <b>Default recommendation</b> for <see cref="OptOutCreate"/>: sliding window,
/// partition by client IP for anonymous callers and by user id when
/// authenticated, <c>PermitLimit = 5</c>, <c>Window = 1m</c>. The opt-out POST
/// is <c>AllowAnonymous</c> (CCPA "Do Not Sell or Share" guest path) so an
/// upper bound is required to keep an unauthenticated flood from saturating the
/// opt-out store and the cookie-issuance path.
/// </para>
/// </remarks>
public static class PrivacyOptOutRateLimitPolicies
{
    /// <summary>
    /// Rate-limit policy guarding <c>POST /privacy/opt-out</c>.
    /// </summary>
    public const string OptOutCreate = "privacy-optout-create";
}
