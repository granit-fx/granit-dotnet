using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.Layouts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Entities.Endpoints.Endpoints;

/// <summary>
/// Range-query handler for the calendar list-view layout. Mounted under
/// <c>{prefix}/{name}/calendar</c> by <c>MapEntitiesEndpoints</c>.
/// </summary>
internal static class CalendarRangeEndpoint
{
    public static RouteGroupBuilder MapCalendarRangeEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/calendar", HandleAsync)
            .WithName("GetEntityCalendarRange")
            .WithSummary("Returns calendar items for one entity within a time window.")
            .WithDescription(
                "Reads the entity's CalendarLayoutDescriptor (selected by the optional ?calendar= name when an entity exposes more than one) "
                + "and returns the events whose Start/End falls inside [from, to]. The range is enforced server-side: a window wider than 366 days "
                + "or with To < From is rejected with 400. Permission gate inherited from the underlying entity's Read permission — same defense-in-depth "
                + "as the manifest endpoint (no calendar-specific permission, rationale: the calendar exposes an aggregate of data the user could already "
                + "see via the list endpoint). Returns an empty list when the entity declares no calendar layout, no item matches, or "
                + "the host has not yet wired a real ICalendarRangeService implementation. FusionCache layers in via a separate story.")
            .Produces<IReadOnlyList<CalendarItemResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<CalendarItemResponse>>, ProblemHttpResult>> HandleAsync(
        string name,
        [AsParameters] CalendarRangeRequest request,
        [FromServices] IEntityDefinitionRegistry registry,
        [FromServices] EntityPermissionResolver permissionResolver,
        [FromServices] ICalendarRangeService calendarRangeService,
        CancellationToken cancellationToken)
    {
        IEntityDefinitionDescriptor? definitionRef = registry.GetByName(name);
        if (definitionRef is null)
        {
            return TypedResults.Problem(
                detail: $"No EntityDefinition is registered with name '{name}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        EntityDefinitionDescriptor descriptor = definitionRef.Descriptor;
        EntityPermissionSnapshot snapshot = await permissionResolver
            .ResolveAsync(descriptor.PermissionGroup, cancellationToken)
            .ConfigureAwait(false);

        if (!snapshot.IsVisible)
        {
            // 403 — defense-in-depth: same shape as the manifest endpoint
            // (a denied read leaks no calendar data, not even row counts).
            return TypedResults.Problem(
                detail: $"You do not have permission to read entity '{name}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        CalendarLayoutDescriptor? layout = SelectCalendarLayout(descriptor, request.Calendar);

        if (layout is null)
        {
            // No matching calendar layout — return empty rather than 404. The
            // wire shape stays predictable for hosts that mount the endpoint
            // generically; a 404 here would surface as a hard error in the
            // EntityListViewSwitcher even though the situation is benign
            // (entity simply has no calendar tab).
            return TypedResults.Ok<IReadOnlyList<CalendarItemResponse>>([]);
        }

        IReadOnlyList<CalendarItemResponse> items = await calendarRangeService
            .GetItemsAsync(descriptor, layout, new CalendarRange(request.From, request.To), cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(items);
    }

    private static CalendarLayoutDescriptor? SelectCalendarLayout(
        EntityDefinitionDescriptor descriptor,
        string? requestedName)
    {
        IEnumerable<CalendarLayoutDescriptor> candidates = descriptor.ListLayouts.OfType<CalendarLayoutDescriptor>();

        if (requestedName is null)
        {
            return candidates.FirstOrDefault();
        }

        // Layout descriptors carry no Name field today (one layout per kind is
        // enforced by AssertUniqueLayoutKinds), so a non-null requestedName
        // matches if and only if the entity has any calendar layout. Reserved
        // for the day multi-instance layouts ship per ADR-042 §4.
        return candidates.FirstOrDefault();
    }
}
