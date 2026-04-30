using System.Globalization;
using System.Security.Claims;
using Granit.Workspaces.Endpoints.Dtos;
using Granit.Workspaces.Endpoints.Internal;
using Granit.Workspaces.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Workspaces.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the workspace tree (Phase 1.D / story #1555).
/// Mounted by <c>MapGranitWorkspacesEndpoints</c>.
/// </summary>
internal static class WorkspacesEndpoints
{
    public static RouteGroupBuilder MapWorkspacesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetTreeAsync)
            .WithName("GetWorkspacesTree")
            .WithSummary("Returns the workspace tree filtered for the requesting user.")
            .WithDescription("Returns every WorkspaceDefinition the user is allowed to see — workspaces, sections, and items gated by a permission the user does NOT hold are absent from the payload (defense in depth, ADR-040). Empty branches (workspaces / sections with zero surviving items) are dropped. Pass ?includeShells=false to omit Framework shells. Cached 5 minutes per (user-perms-hash, culture, includeShells).")
            .Produces<WorkspaceTreeResponse>();

        return group;
    }

    private static async Task<Ok<WorkspaceTreeResponse>> GetTreeAsync(
        [FromQuery] bool? includeShells,
        [FromServices] IWorkspaceRegistry registry,
        [FromServices] WorkspaceFilter filter,
        [FromServices] IFusionCache cache,
        [FromServices] IOptions<WorkspacesEndpointsOptions> options,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        bool shells = includeShells ?? true;
        CultureInfo culture = CultureInfo.CurrentUICulture;
        string cacheKey = WorkspaceCacheKey.Build(user, culture, shells);

        WorkspaceTreeResponse response = await cache.GetOrSetAsync(
            cacheKey,
            async ct => await filter.FilterAsync(registry.All, shells, ct).ConfigureAwait(false),
            new FusionCacheEntryOptions { Duration = options.Value.TreeCacheTtl },
            token: cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(response);
    }
}
