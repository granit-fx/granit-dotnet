using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for the dashboard's widget pool — add, update and remove
/// widget instances. The update endpoint replaces layout / title / config but
/// not WidgetType / MetricName / QueryName / RequiredPermission (those are
/// delete + add).
/// </summary>
internal static class DashboardWidgetEndpoints
{
    public static RouteGroupBuilder MapWidgetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/widgets", AddWidgetAsync)
            .WithName("AddGranitDashboardWidget")
            .WithSummary("Adds a widget instance to the dashboard.")
            .WithDescription(
                "Pins a new widget into the dashboard's widget pool. The widget id is "
                + "allocated server-side. FluentValidation runs first (non-empty type / "
                + "title / config; non-negative position; positive width and height); "
                + "the domain guards stay as defense-in-depth and surface as 422 if hit. "
                + "Returns 201 with the new widget payload, or 404 when the parent "
                + "dashboard is not in the current tenant's scope.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<WidgetInstanceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/widgets/{widgetId:guid}", UpdateWidgetAsync)
            .WithName("UpdateGranitDashboardWidget")
            .WithSummary("Updates a widget's layout, title and config.")
            .WithDescription(
                "Full replacement of the widget's editable fields: Position, Width, "
                + "Height, TitleLocalizationKey, ConfigJson. WidgetType, MetricName, "
                + "QueryName and RequiredPermission stay immutable — switching widget "
                + "kind or rebinding to a different metric/query is delete + add, not "
                + "edit. Returns 200 with the updated widget, 404 when the dashboard "
                + "or the widget id is not in scope, or 422 when the domain guards "
                + "reject the input.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<WidgetInstanceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/widgets/{widgetId:guid}", RemoveWidgetAsync)
            .WithName("RemoveGranitDashboardWidget")
            .WithSummary("Removes a widget instance from the dashboard.")
            .WithDescription(
                "Removes the named widget from the dashboard's pool. Returns 204 on "
                + "successful removal, 404 when the dashboard is not in the current "
                + "tenant's scope, or when the widget id does not belong to that "
                + "dashboard. Idempotent retries on a removed widget therefore receive "
                + "a 404 — callers treat this as benign.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<WidgetInstanceResponse>, ProblemHttpResult>> AddWidgetAsync(
        [FromRoute] Guid id,
        [FromBody] AddWidgetRequest request,
        [FromServices] DashboardWidgetService service,
        CancellationToken cancellationToken)
    {
        DashboardWidgetAddResult result = await service.AddWidgetAsync(
            dashboardId: id,
            widgetType: request.WidgetType,
            position: request.Position,
            width: request.Width,
            height: request.Height,
            titleLocalizationKey: request.TitleLocalizationKey,
            configJson: request.ConfigJson,
            metricName: request.MetricName,
            queryName: request.QueryName,
            requiredPermission: request.RequiredPermission,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            DashboardWidgetAddOutcome.Created =>
                TypedResults.Created(
                    $"/dashboards/{id}/widgets/{result.Widget!.Id}",
                    DashboardInstanceProjection.ToWidgetInstance(result.Widget)),
            DashboardWidgetAddOutcome.NotFound =>
                TypedResults.Problem(
                    detail: $"Dashboard '{id}' not found.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardWidgetAddOutcome.Invalid =>
                TypedResults.Problem(
                    detail: result.InvalidReason,
                    statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => throw new InvalidOperationException($"Unhandled outcome '{result.Outcome}'."),
        };
    }

    private static async Task<Results<Ok<WidgetInstanceResponse>, ProblemHttpResult>> UpdateWidgetAsync(
        [FromRoute] Guid id,
        [FromRoute] Guid widgetId,
        [FromBody] UpdateWidgetRequest request,
        [FromServices] DashboardWidgetService service,
        CancellationToken cancellationToken)
    {
        DashboardWidgetUpdateResult result = await service.UpdateWidgetAsync(
            dashboardId: id,
            widgetId: widgetId,
            position: request.Position,
            width: request.Width,
            height: request.Height,
            titleLocalizationKey: request.TitleLocalizationKey,
            configJson: request.ConfigJson,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            DashboardWidgetUpdateOutcome.Updated =>
                TypedResults.Ok(DashboardInstanceProjection.ToWidgetInstance(result.Widget!)),
            DashboardWidgetUpdateOutcome.DashboardNotFound =>
                TypedResults.Problem(
                    detail: $"Dashboard '{id}' not found.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardWidgetUpdateOutcome.WidgetNotFound =>
                TypedResults.Problem(
                    detail: $"Widget '{widgetId}' not found on dashboard '{id}'.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardWidgetUpdateOutcome.Invalid =>
                TypedResults.Problem(
                    detail: result.InvalidReason,
                    statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => throw new InvalidOperationException($"Unhandled outcome '{result.Outcome}'."),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveWidgetAsync(
        [FromRoute] Guid id,
        [FromRoute] Guid widgetId,
        [FromServices] DashboardWidgetService service,
        CancellationToken cancellationToken)
    {
        DashboardWidgetRemoveResult result = await service.RemoveWidgetAsync(id, widgetId, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            DashboardWidgetRemoveResult.Removed => TypedResults.NoContent(),
            DashboardWidgetRemoveResult.DashboardNotFound =>
                TypedResults.Problem(
                    detail: $"Dashboard '{id}' not found.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardWidgetRemoveResult.WidgetNotFound =>
                TypedResults.Problem(
                    detail: $"Widget '{widgetId}' not found on dashboard '{id}'.",
                    statusCode: StatusCodes.Status404NotFound),
            _ => throw new InvalidOperationException($"Unhandled outcome '{result}'."),
        };
    }
}
