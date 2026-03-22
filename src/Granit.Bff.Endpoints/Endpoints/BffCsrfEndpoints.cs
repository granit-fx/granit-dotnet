using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF CSRF token endpoint. Generates a CSRF token bound to the current session.
/// The SPA must include this token in <c>X-CSRF-Token</c> header on mutating requests.
/// Registered per-frontend under <c>/{pathPrefix}/bff/csrf-token</c>.
/// </summary>
internal static class BffCsrfEndpoints
{
    internal static RouteGroupBuilder MapCsrfEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapPost("/csrf-token", (HttpContext httpContext,
                [FromServices] IBffCsrfTokenGenerator csrfGenerator) =>
                HandleGenerateCsrfTokenAsync(httpContext, frontend, csrfGenerator))
            .WithName($"BffGenerateCsrfToken_{frontend.Name}")
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

#pragma warning disable GRAPI003 // Private handler — not a direct endpoint delegate; services are resolved via lambda
    private static Task<Results<Ok<BffCsrfTokenResponse>, ProblemHttpResult>> HandleGenerateCsrfTokenAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IBffCsrfTokenGenerator csrfGenerator)
    {
        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

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
#pragma warning restore GRAPI003
}

/// <summary>CSRF token response.</summary>
internal sealed record BffCsrfTokenResponse(string CsrfToken);
