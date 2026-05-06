using System.Security.Claims;
using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Authorization;
using Granit.Activities.Endpoints.Dtos;
using Granit.Activities.Endpoints.Internal;
using Granit.Activities.Endpoints.Options;
using Granit.Activities.Endpoints.Permissions;
using Granit.Activities.Persistence;
using Granit.Authorization;
using Granit.Timing;
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
            .WithDescription("Filter by entity (entityType + entityId), assignee (assignedToUserId or 'me'), status (open / done / cancelled), or due-date window (dueAtFrom / dueAtTo, half-open). Ordered by DueAt ascending. Caps page size at ActivitiesEndpointsOptions.MaxPageSize (default 100). Activities pinned to hosts the caller cannot read are filtered out (IActivityHostAuthorizationProvider). Listing peers' activities (assignedToUserId != caller) requires Activities.Activities.ReadOthers.")
            .Produces<ActivityListResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetActivityById")
            .WithSummary("Returns a single activity by id.")
            .WithDescription("Returns 404 if the activity does not exist, is in a different tenant, or its host entity is not readable by the caller.")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("CreateActivity")
            .WithSummary("Creates a new activity in Open state.")
            .WithDescription("Validates the activity type against IActivityRegistry — types from unloaded providers are rejected with 400. The created-by user id is resolved from the authenticated principal — clients cannot impersonate. Emits ActivityAssignedEvent (local) and ActivityAssignedEto (distributed) on success.")
            .Produces<ActivityResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/complete", CompleteAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Execute)
            .WithName("CompleteActivity")
            .WithSummary("Marks the activity as Done.")
            .WithDescription("Idempotent within a single transition — completing an already-terminal activity (Done or Cancelled) returns 409. The completion timestamp is derived server-side from IClock; the actor is resolved from the authenticated principal. Emits ActivityCompletedEvent (local) and ActivityCompletedEto (distributed).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("CancelActivity")
            .WithSummary("Marks the activity as Cancelled.")
            .WithDescription("Append-only — cancelling an already-terminal activity returns 409. The cancellation timestamp is derived server-side from IClock; the actor is resolved from the authenticated principal.")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/assignee", ReassignAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Reassign)
            .WithName("ReassignActivity")
            .WithSummary("Changes the assignee of an open activity.")
            .WithDescription("Allowed only while the activity is Open. Requires the dedicated Reassign permission (separate from Manage so least-privilege roles cannot move work to other users). Emits ActivityReassignedEvent (local) and ActivityReassignedEto (distributed). Setting the same assignee is a no-op (no event raised).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/due-date", RescheduleAsync)
            .RequireAuthorization(ActivitiesPermissions.Activities.Manage)
            .WithName("RescheduleActivity")
            .WithSummary("Updates the due date of an open activity.")
            .WithDescription("Allowed only while the activity is Open. The new due date must lie within ActivitiesEndpointsOptions.MaxFutureRescheduleDays of the server clock. Emits ActivityRescheduledEvent (local) and ActivityRescheduledEto (distributed). Setting the same due date is a no-op (no event raised).")
            .Produces<ActivityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Ok<ActivityListResponse>, ProblemHttpResult>> ListAsync(
        [FromServices] IActivityReader reader,
        [FromServices] ActivityHostAuthorizer hostAuthorizer,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] IOptions<ActivitiesEndpointsOptions> options,
        ClaimsPrincipal user,
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

        // VULN-400 — peer-workload lookups require the dedicated permission.
        Guid? callerId = ResolveCurrentUserId(user);
        if (assignedToUserId is { } target && target != callerId)
        {
            bool granted = await permissionChecker
                .IsGrantedAsync(ActivitiesPermissions.Activities.ReadOthers, cancellationToken)
                .ConfigureAwait(false);
            if (!granted)
            {
                return TypedResults.Problem(
                    detail: "Reading other users' activities requires Activities.Activities.ReadOthers.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        ActivityListFilter filter = new(
            EntityType: entityType,
            EntityId: entityId,
            AssignedToUserId: assignedToUserId,
            Status: status,
            DueAtFrom: dueAtFrom,
            DueAtTo: dueAtTo);

        IReadOnlyList<Activity> rows = await reader.ListAsync(filter, (safePage - 1) * safePageSize, safePageSize, cancellationToken).ConfigureAwait(false);

        // VULN-102 / VULN-202 — drop activities pinned to hosts the caller cannot read.
        rows = await hostAuthorizer.FilterAsync(rows, user, cancellationToken).ConfigureAwait(false);

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
        [FromServices] ActivityHostAuthorizer hostAuthorizer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Activity? activity = await reader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (activity is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        // VULN-102 — return 404 (not 403) on host-auth failure so the host id is not enumerable.
        bool allowed = await hostAuthorizer.CanReadHostAsync(activity.EntityType, activity.EntityId, user, cancellationToken).ConfigureAwait(false);
        if (!allowed)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.Ok(activity.ToResponse());
    }

    private static async Task<Results<Created<ActivityResponse>, ProblemHttpResult>> CreateAsync(
        CreateActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IClock clock,
        [FromServices] IOptions<ActivitiesEndpointsOptions> options,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        Guid? actorId = ResolveCurrentUserId(user);
        if (actorId is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }
        // VULN-203 — clamp DueAt to a sane future bound to prevent stored-state DoS.
        if (!IsWithinFutureBound(request.DueAt, clock, options.Value, out string? boundError))
        {
            return TypedResults.Problem(detail: boundError, statusCode: StatusCodes.Status400BadRequest);
        }
        try
        {
            Activity created = await writer.CreateAsync(
                request.EntityType,
                request.EntityId,
                request.Type,
                request.AssignedToUserId,
                request.DueAt,
                createdByUserId: actorId,
                description: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            string baseUrl = httpContext.Request.PathBase.Add($"/{options.Value.RoutePrefix.Trim('/')}");
            return TypedResults.Created($"{baseUrl}/{created.Id}", created.ToResponse());
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
        [FromServices] IClock clock,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        _ = request;
        Guid? actorId = ResolveCurrentUserId(user);
        if (actorId is null)
        {
            return Task.FromResult<Results<Ok<ActivityResponse>, ProblemHttpResult>>(
                TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized));
        }
        DateTimeOffset at = clock.Normalize(clock.Now);
        return ApplyMutationAsync(id, ct => writer.CompleteAsync(id, actorId.Value, at, ct), reader, cancellationToken);
    }

    private static Task<Results<Ok<ActivityResponse>, ProblemHttpResult>> CancelAsync(
        Guid id,
        CancelActivityRequest request,
        [FromServices] IActivityWriter writer,
        [FromServices] IActivityReader reader,
        [FromServices] IClock clock,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        _ = request;
        Guid? actorId = ResolveCurrentUserId(user);
        if (actorId is null)
        {
            return Task.FromResult<Results<Ok<ActivityResponse>, ProblemHttpResult>>(
                TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized));
        }
        DateTimeOffset at = clock.Normalize(clock.Now);
        return ApplyMutationAsync(id, ct => writer.CancelAsync(id, actorId.Value, at, ct), reader, cancellationToken);
    }

    private static Guid? ResolveCurrentUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out Guid id) ? id : null;
    }

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
        [FromServices] IClock clock,
        [FromServices] IOptions<ActivitiesEndpointsOptions> options,
        CancellationToken cancellationToken)
    {
        if (!IsWithinFutureBound(request.NewDueAt, clock, options.Value, out string? boundError))
        {
            return Task.FromResult<Results<Ok<ActivityResponse>, ProblemHttpResult>>(
                TypedResults.Problem(detail: boundError, statusCode: StatusCodes.Status400BadRequest));
        }
        return ApplyMutationAsync(id, ct => writer.RescheduleAsync(id, request.NewDueAt, ct), reader, cancellationToken);
    }

    // VULN-203 — reject DueAt values past `now + MaxFutureRescheduleDays`.
    // Negative bounds (past dates) are intentionally allowed because the
    // create flow needs to support backfilling activities from imported data;
    // reschedule keeps the same rule for symmetry.
    private static bool IsWithinFutureBound(DateTimeOffset value, IClock clock, ActivitiesEndpointsOptions opts, out string? error)
    {
        DateTimeOffset upper = clock.Normalize(clock.Now).AddDays(opts.MaxFutureRescheduleDays);
        if (value > upper)
        {
            error = $"DueAt cannot be more than {opts.MaxFutureRescheduleDays} days in the future.";
            return false;
        }
        error = null;
        return true;
    }

    // Helper — not a route handler, but the GRAPI003 analyzer treats any
    // interface-typed parameter in *.Endpoints as a candidate, hence the
    // [FromServices] attribute despite having no runtime effect on a private
    // method.
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

