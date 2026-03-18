using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Timeline.Endpoints.Endpoints;

/// <summary>
/// POST/DELETE endpoints for creating and soft-deleting timeline entries.
/// </summary>
internal static class TimelineEntryEndpoints
{
    internal static RouteGroupBuilder MapEntryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{entityType}/{entityId}/entries", PostEntryAsync)
            .WithName("PostTimelineEntry")
            .WithSummary("Posts a new comment, internal note, or system log entry.")
            .WithDescription("Creates a new timeline entry for the specified entity. Supports Comment and InternalNote types (SystemLog is system-only). The body supports Markdown. @mentions in the body auto-subscribe mentioned users as followers and trigger mention notifications. Supports threaded replies via parentEntryId and file attachments via attachmentBlobIds.");

        group.MapDelete("/{entityType}/{entityId}/entries/{entryId:guid}", DeleteEntryAsync)
            .WithName("DeleteTimelineEntry")
            .WithSummary("Soft-deletes a comment or internal note (RGPD right to erasure).")
            .WithDescription("Performs a soft-delete on the timeline entry, preserving the record for audit purposes while hiding the content. Only Comment and InternalNote entries can be deleted. SystemLog entries are immutable (ISO 27001). Returns 404 if the entry does not exist.");

        return group;
    }

    private static async Task<Created<TimelineStreamEntry>> PostEntryAsync(
        string entityType,
        string entityId,
        PostTimelineEntryRequest request,
        [FromServices] ITimelineWriter writer,
        [FromServices] ITimelineFollowerService followerService,
        [FromServices] ITimelineNotifier notifier,
        CancellationToken cancellationToken)
    {
        TimelineEntry entry = await writer.PostEntryAsync(
            entityType, entityId, request.EntryType, request.Body,
            request.ParentEntryId, cancellationToken).ConfigureAwait(false);

        // Parse @mentions and auto-subscribe mentioned users
        IReadOnlyList<string> mentionedUserIds = MentionParser.ExtractMentionedUserIds(entry.Body);
        foreach (string userId in mentionedUserIds)
        {
            await followerService.FollowAsync(userId, entityType, entityId, cancellationToken).ConfigureAwait(false);
        }

        // Notify followers
        IReadOnlyList<string> followerIds = await followerService.GetFollowerIdsAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
        await notifier.NotifyEntryPostedAsync(entry, followerIds, cancellationToken).ConfigureAwait(false);

        // Notify mentioned users separately (may include extra channels like email)
        if (mentionedUserIds.Count > 0)
        {
            await notifier.NotifyMentionedUsersAsync(entry, mentionedUserIds, cancellationToken).ConfigureAwait(false);
        }

        TimelineStreamEntry result = new()
        {
            Id = entry.Id,
            OccurredAt = entry.CreatedAt,
            EntryType = entry.EntryType switch
            {
                TimelineEntryType.Comment => TimelineStreamEntryType.Comment,
                TimelineEntryType.InternalNote => TimelineStreamEntryType.InternalNote,
                TimelineEntryType.SystemLog => TimelineStreamEntryType.SystemLog,
                _ => TimelineStreamEntryType.SystemLog,
            },
            AuthorId = entry.AuthorId,
            AuthorName = entry.AuthorName,
            Body = entry.Body,
            ParentEntryId = entry.ParentEntryId,
        };

        return TypedResults.Created($"/api/timeline/{entityType}/{entityId}/entries/{entry.Id}", result);
    }

#pragma warning disable S1172 // Route parameters bound by ASP.NET Core minimal API
    private static async Task<Results<NoContent, NotFound>> DeleteEntryAsync(
        string entityType,
        string entityId,
        Guid entryId,
        [FromServices] ITimelineWriter writer,
        CancellationToken cancellationToken)
#pragma warning restore S1172
    {
        try
        {
            await writer.DeleteEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }
}
