using System.Security.Claims;
using Granit.Analytics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Rendering;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handler for <c>POST /dashboards/{id}/render</c> — composes the dashboard
/// bundle by dispatching every widget through its registered
/// <see cref="IWidgetInstanceRenderer"/>. Per ADR-039 §3 the underlying
/// <see cref="IDashboardRenderer"/> applies a uniform permission gate and
/// per-widget error isolation, so the endpoint surface stays trivial — load,
/// build context, dispatch, project.
/// </summary>
internal static class DashboardRenderEndpoints
{
    public static RouteGroupBuilder MapRenderEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/render", RenderAsync)
            .WithName("RenderGranitDashboard")
            .WithSummary("Renders a dashboard's widget pool into one bundle response.")
            .WithDescription(
                "Loads the persisted dashboard, builds the per-render context "
                + "(tenant, principal, period, locale, dashboard filters, resolved "
                + "entity aliases) and dispatches every widget through its registered "
                + "IWidgetInstanceRenderer. Each widget surfaces independently as "
                + "Snapshot / Unavailable / Error — one bad widget cannot 500 the "
                + "whole render. Multi-tenant filtered through the DbContext (404 "
                + "for cross-tenant ids — never leaks existence).")
            .RequireAuthorization(DashboardsPermissions.Instances.Read)
            .Produces<DashboardRenderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<DashboardRenderResponse>, ProblemHttpResult>> RenderAsync(
        [FromRoute] Guid id,
        [FromBody] DashboardRenderRequest? request,
        [FromServices] DashboardReader reader,
        [FromServices] IDashboardRenderer renderer,
        [FromServices] IDashboardDefinitionRegistry definitionRegistry,
        [FromServices] ICurrentTenant? currentTenant,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        DashboardRenderRequest body = request ?? new DashboardRenderRequest();

        // Period bound consistency is enforced by DashboardRenderRequestValidator
        // (FluentValidationAutoEndpointFilter runs before this handler — see
        // MapGranitGroup wiring).

        Dashboard? dashboard = await reader.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (dashboard is null)
        {
            return TypedResults.Problem(
                detail: $"Dashboard '{id}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        ResolvedPeriod? period = DashboardRenderProjection.TryBuildResolvedPeriod(body);

        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;

        WidgetRenderContext context = new(
            TenantId: tenantId,
            User: user,
            Period: period,
            Locale: body.Locale ?? "en",
            DashboardFilters: body.Filters ?? new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

        // P2.1 multi-view dispatch — resolve the active view and the matching
        // widget pool. Single-view dashboards short-circuit to dashboard.Widgets
        // with ActiveViewName: null; non-entry views materialise ephemeral
        // WidgetInstances from the source DashboardDefinition.
        IDashboardDefinitionDescriptor? descriptor = dashboard.SourceDefinitionName is { } sn
            ? definitionRegistry.Find(sn)
            : null;
        ResolvedRenderTarget target = ActiveViewResolver.Resolve(dashboard, descriptor, body.ViewName);

        DashboardRenderResult result = await renderer
            .RenderAsync(dashboard.Id, target.Widgets, context, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(DashboardRenderProjection.ToResponse(
            result, dashboard, target, definitionRegistry, body.PeriodToken));
    }
}
