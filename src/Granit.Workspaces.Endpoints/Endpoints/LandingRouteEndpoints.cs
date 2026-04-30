using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Workspaces.Endpoints.Dtos;
using Granit.Workspaces.Endpoints.Landing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Workspaces.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the landing-route surface (story #1556).
/// Mounted by <c>MapGranitLandingRouteEndpoints</c> under the supplied prefix.
/// </summary>
internal static class LandingRouteEndpoints
{
    public static RouteGroupBuilder MapLandingRouteEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAsync)
            .WithName("GetLandingRoute")
            .WithSummary("Resolves the requesting user's landing route via the 5-tier precedence chain.")
            .WithDescription("Precedence per ADR-048: personal-sticky > personal-pinned > role-default > tenant-default > framework. Each tier may be null (provider not configured, no preference) — the resolver falls through. Routes that fail the URL whitelist (configured prefixes) or the access guard are also skipped to the next tier. The framework fallback is configured (not user-supplied) and always wins as the closing tier.")
            .Produces<LandingRouteResponse>();

        group.MapPut("/pinned", SetPinnedAsync)
            .WithName("SetPinnedLandingRoute")
            .WithSummary("Pins (or clears) the requesting user's preferred landing route.")
            .WithDescription("The pinned route is consulted at the second-highest precedence tier (after personal-sticky). Pass null in the body to clear the pin. Routes that don't match the URL whitelist are rejected with 400.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Ok<LandingRouteResponse>> GetAsync(
        [FromServices] LandingRouteResolver resolver,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        LandingRouteResult result = await resolver.ResolveAsync(user, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new LandingRouteResponse(result.Route, result.Source));
    }

    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> SetPinnedAsync(
        SetPinnedLandingRouteRequest request,
        [FromServices] ILandingRouteStore store,
        [FromServices] LandingRouteResolver resolver,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // null clears the pin — accepted unconditionally so users can opt out.
        if (request.Route is not null && !resolver.IsAllowedRoute(request.Route))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["route"] = ["Route must start with one of the configured allowed prefixes."],
            });
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        await store.SetPinnedAsync(userId, tenantId, request.Route, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
