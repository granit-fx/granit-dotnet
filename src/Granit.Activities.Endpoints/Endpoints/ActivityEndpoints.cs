using Granit.Activities.Abstractions;
using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Dtos;
using Granit.Activities.Endpoints.Internal;
using Granit.Activities.Endpoints.Options;
using Granit.Activities.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Activities.Endpoints.Endpoints;

/// <summary>
/// CRUD + lifecycle transition endpoints for the <see cref="Activity"/>
/// aggregate. All endpoints inherit the <c>Activities.Activities.Read</c> gate
/// from the route group; per-endpoint write permissions are layered on top.
/// </summary>
internal static class ActivityEndpoints
{
    internal static RouteGroupBuilder MapActivityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListActivities")
            .WithSummary("Returns a paginated list of activities matching the query filter.")
            .WithDescription("Filter by entity (entityType + entityId), assignee (assignedToUserId or 'me'), status (open / done / cancelled), or due-date window (dueAtFrom / dueAtTo, half-open). Ordered by DueAt ascending. Caps page size at ActivitiesEndpointsOptions.MaxPageSize (default 100).")
            .Produces<ActivityListResponse>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetActivityById")
            .WithSummary("Returns a single activity by id.")
            .WithDescription("Returns 404 if the activity does not exist or is in a different tenant.")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("CreateActivity")
            .WithSummary("Creates a new activity in Open state.")
            .WithDescription("Validates the activity type against IActivityRegistry — types from unloaded providers are rejected with 400. Emits ActivityAssignedEvent (local) and ActivityAssignedEto (distributed) on success.")
            .Produces<ActivityResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/complete", CompleteAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Execute)
            .WithName("CompleteActivity")
            .WithSummary("Marks the activity as Done.")
            .WithDescription("Idempotent within a single transition — completing an already-terminal activity (Done or Cancelled) returns 409. Emits ActivityCompletedEvent (local) and ActivityCompletedEto (distributed).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("CancelActivity")
            .WithSummary("Marks the activity as Cancelled.")
            .WithDescription("Append-only — cancelling an already-terminal activity returns 409. Cancel does not propagate to the integration bus (internal lifecycle only).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/assignee", ReassignAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("ReassignActivity")
            .WithSummary("Changes the assignee of an open activity.")
            .WithDescription("Allowed only while the activity is Open. Emits ActivityReassignedEvent. Setting the same assignee is a no-op (no event raised).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/due-date", RescheduleAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("RescheduleActivity")
            .WithSummary("Updates the due date of an open activity.")
            .WithDescription("Allowed only while the activity is Open. Emits ActivityRescheduledEvent. Setting the same due date is a no-op (no event raised).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Ok<ActivityListResponse>> ListAsync(
        [FromServices] IActivityReader reader,
        [FromServices] IOptions<ActivitiesEndpointsOptions> options,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] ActivityStatusFilter? status = null,
        [FromQuery] DateTimeOffset? dueAtFrom = null,
        [FromQuery] DateTimeOffset? dueAtTo = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        ActivitiesEndpointsOptions opts = options.Value;
        int safePage = page is { } p && p > 0 ? p : 1;
        int safePageSize = pageSize is { } ps && ps > 0
            ? Math.Min(ps, opts.MaxPageSize)
            : opts.DefaultPageSize;

        ActivityListFilter filter = new(
            EntityType: entityType,
            EntityId: entityId,
            AssignedToUserId: assignedToUserId,
            Status: status,
            DueAtFrom: dueAtFrom,
            DueAtTo: dueAtTo);

        IReadOnlyList<Activity> rows = await reader.ListAsync(filter, (safePage - 1) * safePageSize, safePageSize, cancellationToken).ConfigureAwait(false);
        int total = await reader.CountAsync(filter, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new ActivityListResponse(
            Items: [.. rows.Select(a => a.ToResponse())],
            TotalCount: total,
            Page: safePage,
            PageSize: safePageSize));
    }

    private static async Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken)
    {
        Activity? activity = await reader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return activity is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(activity.ToResponse());
    }

    private static async Task<Results<Created<ActivityResponse>, ProblemHttpResult>> CreateAsync(
        CreateActivityRequest request,
        [FromServices] IActivityWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            Activity created = await writer.CreateAsync(
                request.EntityType,
                request.EntityId,
                request.Type,
                request.AssignedToUserId,
                request.DueAt,
                createdByUserId: null,
                description: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return TypedResults.Created($"/api/activities/{created.Id}", created.ToResponse());
        }
        catch (ArgumentException ex) when (ex.ParamName == "type")
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> CompleteAsync(
        Guid id,
        CompleteActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken) =>
        ApplyMutationAsync(id, ct => writer.CompleteAsync(id, completedByUserId: Guid.Empty, request.CompletedAt, ct), reader, cancellationToken);

    private static Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> CancelAsync(
        Guid id,
        CancelActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken) =>
        ApplyMutationAsync(id, ct => writer.CancelAsync(id, cancelledByUserId: Guid.Empty, request.CancelledAt, ct), reader, cancellationToken);

    private static Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> ReassignAsync(
        Guid id,
        ReassignActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken) =>
        ApplyMutationAsync(id, ct => writer.ReassignAsync(id, request.NewAssigneeUserId, ct), reader, cancellationToken);

    private static Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> RescheduleAsync(
        Guid id,
        RescheduleActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken) =>
        ApplyMutationAsync(id, ct => writer.RescheduleAsync(id, request.NewDueAt, ct), reader, cancellationToken);

    // Private helper — not a route handler, but the architecture test
    // (EndpointParameterBindingTests) does a static text scan and treats any
    // interface-typed parameter in *.Endpoints as a candidate, so the
    // [FromServices] attribute is applied for parity even though it has no
    // runtime effect on a private method.
    private static async Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> ApplyMutationAsync(
        Guid activityId,
        Func<CancellationToken, Task> mutate,
        [FromServices] IActivityReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await mutate(cancellationToken).ConfigureAwait(false);
            Activity? after = await reader.GetByIdAsync(activityId, cancellationToken).ConfigureAwait(false);
            return after is null
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
                : TypedResults.Ok(after.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
