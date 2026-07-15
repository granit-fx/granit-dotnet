using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Builds RFC 7807 problem responses whose <c>detail</c> is always a localized,
/// caller-safe message — never a raw <see cref="System.Exception.Message"/>.
/// </summary>
/// <remarks>
/// Centralises the rule that account/role endpoints must not leak internal exception text
/// (ASP.NET Identity validator descriptions, EF messages, GUIDs) onto the wire. Endpoints
/// classify the failure into a stable localization key and delegate the response shape here.
/// </remarks>
internal static class ProblemFactory
{
    /// <summary>
    /// Returns a <see cref="ProblemHttpResult"/> whose detail is the localized message for
    /// <paramref name="key"/> (falling back to <paramref name="fallback"/> when the localizer
    /// is unavailable), with the given <paramref name="statusCode"/>.
    /// </summary>
    public static ProblemHttpResult Localized(HttpContext httpContext, string key, string fallback, int statusCode) =>
        TypedResults.Problem(
            detail: AccountEndpointMessages.Localize(httpContext, key, fallback),
            statusCode: statusCode);
}
