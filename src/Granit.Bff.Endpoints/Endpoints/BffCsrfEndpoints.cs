using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF CSRF token endpoint. Generates a CSRF token bound to the current session.
/// The SPA must include this token in <c>X-CSRF-Token</c> header on mutating requests.
/// </summary>
internal static class BffCsrfEndpoints
{
    internal static RouteGroupBuilder MapCsrfEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/csrf-token", HandleGenerateCsrfTokenAsync)
            .WithName("BffGenerateCsrfToken")
            .WithSummary("Generates a CSRF token for the current BFF session.")
            .WithDescription(
                "Reads the session cookie to identify the session, generates an HMAC-SHA256 based "
                + "CSRF token, and returns it in the response body. The SPA must include this token "
                + "in the X-CSRF-Token header on all POST/PUT/DELETE/PATCH requests. "
                + "Returns 401 if no valid session exists.")
            .Produces<BffCsrfTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static Task<Results<Ok<BffCsrfTokenResponse>, ProblemHttpResult>> HandleGenerateCsrfTokenAsync(
        HttpContext httpContext,
        [FromServices] IOptions<GranitBffOptions> options,
        [FromServices] IBffCsrfTokenGenerator csrfGenerator)
    {
        GranitBffOptions bffOptions = options.Value;
        string? sessionId = httpContext.Request.Cookies[bffOptions.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            Results<Ok<BffCsrfTokenResponse>, ProblemHttpResult> result = TypedResults.Problem(
                detail: "No active BFF session.",
                statusCode: StatusCodes.Status401Unauthorized);
            return Task.FromResult(result);
        }

        string token = csrfGenerator.Generate(sessionId);
        Results<Ok<BffCsrfTokenResponse>, ProblemHttpResult> ok = TypedResults.Ok(new BffCsrfTokenResponse(token));
        return Task.FromResult(ok);
    }
}

/// <summary>CSRF token response.</summary>
internal sealed record BffCsrfTokenResponse(string CsrfToken);
