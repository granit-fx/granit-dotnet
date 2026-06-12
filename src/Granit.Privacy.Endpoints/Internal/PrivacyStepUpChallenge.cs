using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Granit.Privacy.Endpoints.Internal;

/// <summary>
/// Helpers for the step-up authentication gate applied to privacy-export
/// download endpoints. A long-lived session cookie is not enough to download
/// a GDPR archive — the caller must have re-authenticated recently enough that
/// the OIDC <c>auth_time</c> claim falls inside the configured freshness window.
/// </summary>
/// <remarks>
/// <para>
/// On failure the endpoint emits a <c>401 Unauthorized</c> with a
/// <c>WWW-Authenticate: Bearer error="step_up", acr_values="urn:granit:step-up"</c>
/// header so an OIDC-aware BFF can transparently redirect the user back through
/// the identity provider for a fresh authentication before retrying the download.
/// </para>
/// </remarks>
internal static class PrivacyStepUpChallenge
{
    /// <summary>
    /// ACR value advertised in the <c>WWW-Authenticate</c> challenge header.
    /// </summary>
    internal const string StepUpAcrValue = "urn:granit:step-up";

    private const string AuthTimeClaim = "auth_time";

    /// <summary>
    /// Returns <see langword="true"/> when the caller's <c>auth_time</c> claim
    /// is present and within <paramref name="maxAge"/> of <paramref name="now"/>.
    /// </summary>
    internal static bool IsAuthFresh(HttpContext httpContext, TimeSpan maxAge, DateTimeOffset now)
    {
        Claim? authTimeClaim = httpContext.User.FindFirst(AuthTimeClaim);
        if (authTimeClaim is null)
        {
            return false;
        }

        if (!long.TryParse(authTimeClaim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long authTimeUnix))
        {
            return false;
        }

        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeUnix);
        return now - authTime <= maxAge;
    }

    /// <summary>
    /// Sets the <c>WWW-Authenticate</c> challenge header for a step-up retry on
    /// <paramref name="httpContext"/>'s response and returns the standard problem
    /// detail message — call sites then return a 401 ProblemHttpResult that picks
    /// the header up.
    /// </summary>
    internal static string SetStepUpChallengeHeader(HttpContext httpContext, TimeSpan maxAge)
    {
        string header = $"Bearer error=\"step_up\", acr_values=\"{StepUpAcrValue}\", max_age=\"{(int)maxAge.TotalSeconds}\"";
        httpContext.Response.Headers.WWWAuthenticate = header;
        return $"Step-up authentication required: re-authenticate within the last {(int)maxAge.TotalMinutes} minute(s).";
    }
}
