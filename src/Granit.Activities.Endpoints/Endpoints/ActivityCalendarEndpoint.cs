using System.Security.Claims;
using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Authorization;
using Granit.Activities.Endpoints.Dtos;
using Granit.Activities.Endpoints.Internal;
using Granit.Activities.Endpoints.Options;
using Granit.Activities.Persistence;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Activities.Endpoints.Endpoints;

/// <summary>
/// Cross-entity activities calendar — returns one stream of
/// <see cref="ActivityCalendarItemResponse"/> entries spanning every entity
/// type the caller can see, optionally filtered by assignee / type / status.
/// Mounted under <c>{prefix}/calendar</c> by the route group.
/// </summary>
internal static class ActivityCalendarEndpoint
{
    internal static RouteGroupBuilder MapActivityCalendarEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/calendar", HandleAsync)
            .WithName("GetActivityCalendar")
            .WithSummary("Returns activities positioned on the cross-entity calendar within a time window.")
            .WithDescription(
                "Spans every entity type the caller can read. The window must be <= ActivitiesEndpointsOptions.MaxCalendarRangeDays "
                + "(default 90 days) and (To > From) — otherwise 400. Filter by assignee ('me' resolves from the JWT 'sub' claim or "
                + "any string-form Guid), entityType (polymorphic FK), type (comma-separated), or status (defaults to OpenOrOverdue). "
                + "FusionCache TTL is 1 minute by default; the cache key includes the resolved tenant id, the user permission hash, "
                + "the window, and every filter. Per-tenant eviction tags drop every cached calendar window when an Activity row "
                + "changes (handler in this same package). Returns 304 on If-None-Match match.")
            .Produces<IReadOnlyList<ActivityCalendarItemResponse>>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<ActivityCalendarItemResponse>>, StatusCodeHttpResult, ProblemHttpResult>> HandleAsync(
        [AsParameters] ActivityCalendarRequest request,
        [FromServices] IActivityReader reader,
        [FromServices] IActivityRegistry registry,
        [FromServices] IFusionCache cache,
        [FromServices] IOptions<ActivitiesEndpointsOptions> options,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IClock clock,
        [FromServices] ActivityHostAuthorizer hostAuthorizer,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ActivitiesEndpointsOptions opts = options.Value;

        if (request.To <= request.From)
        {
            return TypedResults.Problem(detail: "Calendar window must satisfy To > From.", statusCode: StatusCodes.Status400BadRequest);
        }
        if ((request.To - request.From).TotalDays > opts.MaxCalendarRangeDays)
        {
            return TypedResults.Problem(
                detail: $"Calendar window cannot exceed {opts.MaxCalendarRangeDays} days.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Guid? assignee = ResolveAssignee(request.Assignee, user);
        if (request.Assignee is { Length: > 0 } && assignee is null)
        {
            return TypedResults.Problem(
                detail: "Assignee must be 'me' or a Guid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // VULN-204 — snap the window to a fixed grid before the cache key is
        // composed, so an attacker cannot shift `from`/`to` by one second per
        // call to defeat the FusionCache TTL.
        DateTimeOffset snappedFrom = SnapWindow(request.From, opts.CalendarWindowSnapMinutes, roundUp: false);
        DateTimeOffset snappedTo = SnapWindow(request.To, opts.CalendarWindowSnapMinutes, roundUp: true);

        ActivityListFilter filter = new(
            EntityType: request.EntityType,
            EntityId: null,
            AssignedToUserId: assignee,
            Status: request.Status,
            DueAtFrom: snappedFrom,
            DueAtTo: snappedTo);

        HashSet<string>? typeFilter = ParseTypeFilter(request.Type);

        string tenantSegment = currentTenant.IsAvailable ? currentTenant.Id?.ToString() ?? "global" : "global";
        string cacheKey = ActivityCalendarCacheKey.Build(
            tenantSegment, user, snappedFrom, snappedTo,
            assigneeFilter: request.Assignee, entityTypeFilter: request.EntityType,
            typeFilter: request.Type, statusFilter: request.Status?.ToString());
        string evictionTag = ActivityCalendarCacheKey.EvictionTag(currentTenant.IsAvailable ? currentTenant.Id : null);

        DateTimeOffset now = clock.Normalize(clock.Now);
        IReadOnlyList<ActivityCalendarItemResponse> items = await cache.GetOrSetAsync(
            cacheKey,
            async ct => await ProjectAsync(reader, registry, hostAuthorizer, user, filter, typeFilter, opts, now, ct).ConfigureAwait(false),
            new FusionCacheEntryOptions { Duration = opts.CalendarCacheTtl },
            tags: [evictionTag],
            token: cancellationToken)
            .ConfigureAwait(false);

        string etag = ActivityCalendarETag.Compute(items);
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.IfNoneMatch.ToString() is { Length: > 0 } ifNoneMatch
            && string.Equals(ifNoneMatch, etag, StringComparison.Ordinal))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Ok(items);
    }

    private static async Task<IReadOnlyList<ActivityCalendarItemResponse>> ProjectAsync(
        IActivityReader reader,
        IActivityRegistry registry,
        ActivityHostAuthorizer hostAuthorizer,
        ClaimsPrincipal user,
        ActivityListFilter filter,
        HashSet<string>? typeFilter,
        ActivitiesEndpointsOptions opts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Cap the projection at the configured max page size — calendar windows
        // shouldn't render thousands of activities at once; the host can enlarge
        // MaxPageSize if a multi-month wall-board is required.
        IReadOnlyList<Activity> rows = await reader.ListAsync(filter, skip: 0, take: opts.MaxPageSize, cancellationToken)
            .ConfigureAwait(false);

        // VULN-202 — drop activities whose host the caller cannot read.
        rows = await hostAuthorizer.FilterAsync(rows, user, cancellationToken).ConfigureAwait(false);

        List<ActivityCalendarItemResponse> result = new(rows.Count);
        foreach (Activity activity in rows)
        {
            if (typeFilter is not null && !typeFilter.Contains(activity.Type))
            {
                continue;
            }

            DateTimeOffset? end = registry.TryGet(activity.Type, out ActivityType? type) && type.DefaultDurationMinutes is { } minutes
                ? activity.DueAt.AddMinutes(minutes)
                : null;

            string color = activity.Status switch
            {
                ActivityStatus.Done => "done",
                ActivityStatus.Cancelled => "cancelled",
                _ when activity.DueAt < now => "overdue",
                _ => "open",
            };

            result.Add(new ActivityCalendarItemResponse(
                Id: activity.Id,
                Start: activity.DueAt,
                End: end,
                Title: activity.Type,
                Color: color,
                Type: activity.Type,
                Status: activity.Status,
                EntityType: activity.EntityType,
                EntityId: activity.EntityId,
                AssignedToUserId: activity.AssignedToUserId));
        }

        return result;
    }

    private static Guid? ResolveAssignee(string? assignee, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(assignee))
        {
            return null;
        }
        if (string.Equals(assignee, "me", StringComparison.OrdinalIgnoreCase))
        {
            string? sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out Guid id) ? id : null;
        }
        return Guid.TryParse(assignee, out Guid g) ? g : null;
    }

    // VULN-204 — round the timestamp down (or up) to the nearest snap-minutes
    // boundary so the cache key is stable across attacker-shifted windows.
    private static DateTimeOffset SnapWindow(DateTimeOffset value, int snapMinutes, bool roundUp)
    {
        if (snapMinutes <= 0)
        {
            return value;
        }
        long snapTicks = TimeSpan.FromMinutes(snapMinutes).Ticks;
        long ticks = value.UtcTicks;
        long remainder = ticks % snapTicks;
        if (remainder == 0)
        {
            return value;
        }
        long adjusted = roundUp
            ? ticks + (snapTicks - remainder)
            : ticks - remainder;
        return new DateTimeOffset(adjusted, TimeSpan.Zero).ToOffset(value.Offset);
    }

    private static HashSet<string>? ParseTypeFilter(string? typeQuery)
    {
        if (string.IsNullOrWhiteSpace(typeQuery))
        {
            return null;
        }
        string[] parts = typeQuery.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts.ToHashSet(StringComparer.Ordinal);
    }
}
