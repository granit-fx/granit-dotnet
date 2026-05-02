using Granit.Authorization;
using Granit.Authorization.Extensions;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Users;
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
            .Produces<PagedResult<TimelineStreamEntryResponse>>()
            .AllowHostAccess();

        return group;
    }

    private static async Task<Ok<PagedResult<TimelineStreamEntryResponse>>> GetStreamAsync(
        string entityType,
        string entityId,
        [FromServices] ITimelineReader reader,
        [FromServices] IReactionReader reactionReader,
        [FromServices] ICurrentUserService currentUser,
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

        // C3 — batch-load reactions for the visible entries in one round trip,
        // then attach the per-emoji summary to each response. Entries without
        // reactions get a null Reactions field (omitted on the wire).
        Guid[] entryIds = [.. result.Items.Select(e => e.Id)];
        IReadOnlyList<Reaction> reactions = await reactionReader
            .GetByEntriesAsync(entryIds, cancellationToken)
            .ConfigureAwait(false);
        Guid? currentUserId = Guid.TryParse(currentUser.UserId, out Guid uid) ? uid : null;
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> byEntry =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId);

        PagedResult<TimelineStreamEntryResponse> mapped = new(
            [.. result.Items.Select(e => TimelineResponseMapper.ToResponse(
                e,
                byEntry.TryGetValue(e.Id, out IReadOnlyDictionary<string, ReactionAggregateResponse>? r) ? r : null))],
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }
}
