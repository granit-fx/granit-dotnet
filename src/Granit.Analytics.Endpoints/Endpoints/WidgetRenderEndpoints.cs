using System.Security.Claims;
using Granit.Analytics.Endpoints.Dtos.Widgets;
using Granit.Analytics.Endpoints.Internal;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Analytics.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for the per-widget render endpoints
/// (<c>POST /widgets/{kind}/render</c>) — a typed
/// <see cref="Granit.Dashboards.WidgetDefinition"/> in, a
/// <see cref="DashboardRenderedWidgetResponse"/> out (same shape the bundle
/// path emits, so the frontend can wrap the same snapshot widgets across the
/// definition path and the bundle path).
/// </summary>
/// <remarks>
/// <para>
/// One endpoint per kind keeps the OpenAPI typed end-to-end — the alternative
/// (a single polymorphic <c>WidgetDefinition</c> body) flows fine through
/// System.Text.Json's <c>[JsonPolymorphic]</c> discriminator but Swagger /
/// Scalar struggle to render it without authoring a custom schema transformer.
/// </para>
/// <para>
/// Permission gating mirrors the bundle path: the typed renderer surface
/// raises <see cref="Granit.Analytics.Metrics.WidgetSnapshotStatus.Unavailable"/>
/// when the user lacks the widget-level permission — never a 403 for the
/// whole call. Authorization on the route group itself is still required to
/// reach the handler.
/// </para>
/// </remarks>
internal static class WidgetRenderEndpoints
{
    internal static RouteGroupBuilder MapWidgetRenderEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/widgets/kpi/render", RenderKpiAsync)
            .WithName("RenderGranitKpiWidget")
            .WithSummary("Renders a single KPI widget definition.")
            .WithDescription(
                "Used by the dashboard composer / catalogue / preview paths to fetch a "
                + "KPI snapshot without persisting the widget. Body carries the typed "
                + "KpiWidgetDefinition (with its embedded Datasource — MetricDatasource "
                + "or QueryAggregateDatasource) plus an optional render context. "
                + "Returns the same DashboardRenderedWidgetResponse shape the bundle path emits.")
            .Produces<DashboardRenderedWidgetResponse>()
            .ProducesValidationProblem();

        group.MapPost("/widgets/chart/render", RenderChartAsync)
            .WithName("RenderGranitChartWidget")
            .WithSummary("Renders a single chart widget definition.")
            .WithDescription(
                "Used by the dashboard composer / catalogue / preview paths to fetch a "
                + "chart snapshot without persisting the widget. Body carries the typed "
                + "ChartWidgetDefinition plus an optional render context. Returns the "
                + "same DashboardRenderedWidgetResponse shape the bundle path emits.")
            .Produces<DashboardRenderedWidgetResponse>()
            .ProducesValidationProblem();

        group.MapPost("/widgets/table/render", RenderTableAsync)
            .WithName("RenderGranitTableWidget")
            .WithSummary("Renders a single table widget definition.")
            .WithDescription(
                "Used by the dashboard composer / catalogue / preview paths to fetch a "
                + "table snapshot without persisting the widget. Body carries the typed "
                + "TableWidgetDefinition plus an optional render context.")
            .Produces<DashboardRenderedWidgetResponse>()
            .ProducesValidationProblem();

        group.MapPost("/widgets/pivot/render", RenderPivotAsync)
            .WithName("RenderGranitPivotWidget")
            .WithSummary("Renders a single pivot widget definition.")
            .WithDescription(
                "Used by the dashboard composer / catalogue / preview paths to fetch a "
                + "pivot snapshot without persisting the widget. Body carries the typed "
                + "PivotWidgetDefinition plus an optional render context.")
            .Produces<DashboardRenderedWidgetResponse>()
            .ProducesValidationProblem();

        group.MapPost("/widgets/map/render", RenderMapAsync)
            .WithName("RenderGranitMapWidget")
            .WithSummary("Renders a single map widget definition.")
            .WithDescription(
                "Used by the dashboard composer / catalogue / preview paths to fetch a "
                + "map snapshot without persisting the widget. Body carries the typed "
                + "MapWidgetDefinition plus an optional render context.")
            .Produces<DashboardRenderedWidgetResponse>()
            .ProducesValidationProblem();

        return group;
    }

    private static Task<Ok<DashboardRenderedWidgetResponse>> RenderKpiAsync(
        [FromBody] KpiWidgetRenderRequest request,
        [FromServices] IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        WidgetRenderHelper.RenderAsync(request.Definition, request.Context, renderer, user, cancellationToken);

    private static Task<Ok<DashboardRenderedWidgetResponse>> RenderChartAsync(
        [FromBody] ChartWidgetRenderRequest request,
        [FromServices] IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        WidgetRenderHelper.RenderAsync(request.Definition, request.Context, renderer, user, cancellationToken);

    private static Task<Ok<DashboardRenderedWidgetResponse>> RenderTableAsync(
        [FromBody] TableWidgetRenderRequest request,
        [FromServices] IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        WidgetRenderHelper.RenderAsync(request.Definition, request.Context, renderer, user, cancellationToken);

    private static Task<Ok<DashboardRenderedWidgetResponse>> RenderPivotAsync(
        [FromBody] PivotWidgetRenderRequest request,
        [FromServices] IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        WidgetRenderHelper.RenderAsync(request.Definition, request.Context, renderer, user, cancellationToken);

    private static Task<Ok<DashboardRenderedWidgetResponse>> RenderMapAsync(
        [FromBody] MapWidgetRenderRequest request,
        [FromServices] IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        WidgetRenderHelper.RenderAsync(request.Definition, request.Context, renderer, user, cancellationToken);
}
