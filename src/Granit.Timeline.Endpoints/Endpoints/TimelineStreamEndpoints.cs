using Granit.Authorization.Abstractions;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Timeline.Endpoints.Endpoints;

/// <summary>
/// GET endpoints for querying the paginated activity stream.
/// </summary>
internal static class TimelineStreamEndpoints
{
    internal static RouteGroupBuilder MapStreamEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{entityType}/{entityId}", GetStreamAsync)
            .WithName("GetTimelineStream")
            .WithSummary("Returns the paginated activity stream for an entity, newest first.")
            .WithDescription("Returns comments, internal notes (staff-only, requires Timeline.InternalNotes.Read), and system log entries. Supports pagination. Soft-deleted entries are excluded.")
            .Produces<PagedResult<TimelineStreamEntry>>();

        return group;
    }

    private static async Task<Ok<PagedResult<TimelineStreamEntry>>> GetStreamAsync(
        string entityType,
        string entityId,
        [FromServices] ITimelineReader reader,
        [FromServices] IPermissionChecker permissionChecker,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PagedResult<TimelineStreamEntry> result = await reader.GetStreamAsync(entityType, entityId, page, pageSize, cancellationToken).ConfigureAwait(false);

        // VULN-101: Filter InternalNote entries unless user has staff permission
        bool canReadInternalNotes = await permissionChecker.IsGrantedAsync(
            TimelinePermissions.InternalNotes.Read, cancellationToken).ConfigureAwait(false);

        if (!canReadInternalNotes)
        {
            var filtered = result.Items
                .Where(e => e.EntryType != TimelineStreamEntryType.InternalNote)
                .ToList();

            result = new PagedResult<TimelineStreamEntry>(filtered, result.TotalCount, result.HasMore);
        }

        return TypedResults.Ok(result);
    }
}
