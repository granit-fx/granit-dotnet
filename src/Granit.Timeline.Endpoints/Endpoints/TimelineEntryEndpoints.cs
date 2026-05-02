using Granit.Authorization;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Timeline.Internal;
using Granit.Users;
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
    private const int MaxMentionsPerEntry = 10;

    internal static RouteGroupBuilder MapEntryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{entityType}/{entityId}/entries", PostEntryAsync)
            .RequireAuthorization(TimelinePermissions.Entries.Create)
            .WithName("PostTimelineEntry")
            .WithSummary("Posts a new comment, internal note, or system log entry.")
            .WithDescription("Creates a new timeline entry for the specified entity. Supports Comment and InternalNote types (SystemLog is system-only). The body supports Markdown. @mentions in the body trigger one-time mention notifications (max 10 per entry). Supports threaded replies via parentEntryId.")
            .Produces<TimelineStreamEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapDelete("/{entityType}/{entityId}/entries/{entryId:guid}", DeleteEntryAsync)
            .RequireAuthorization(TimelinePermissions.Entries.Manage)
            .WithName("DeleteTimelineEntry")
            .WithSummary("Soft-deletes a comment or internal note (GDPR right to erasure).")
            .WithDescription("Performs a soft-delete on the timeline entry. Only the author or users with Timeline.Entries.Manage permission can delete. SystemLog entries are immutable (ISO 27001). Returns 404 if the entry does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Created<TimelineStreamEntryResponse>> PostEntryAsync(
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

        // VULN-205: Parse @mentions with cap — send one-time notification only, no auto-follow
        IReadOnlyList<string> mentionedUserIds = MentionParser.ExtractMentionedUserIds(entry.Body);
        if (mentionedUserIds.Count > MaxMentionsPerEntry)
        {
            mentionedUserIds = [.. mentionedUserIds.Take(MaxMentionsPerEntry)];
        }

        // Notify followers
        IReadOnlyList<string> followerIds = await followerService.GetFollowerIdsAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
        await notifier.NotifyEntryPostedAsync(entry, followerIds, cancellationToken).ConfigureAwait(false);

        // Notify mentioned users separately (may include extra channels like email)
        if (mentionedUserIds.Count > 0)
        {
            await notifier.NotifyMentionedUsersAsync(entry, mentionedUserIds, cancellationToken).ConfigureAwait(false);
        }

        TimelineStreamEntryResponse result = new(
            entry.Id,
            entry.CreatedAt,
            entry.EntryType switch
            {
                TimelineEntryType.Comment => TimelineStreamEntryType.Comment,
                TimelineEntryType.InternalNote => TimelineStreamEntryType.InternalNote,
                TimelineEntryType.SystemLog => TimelineStreamEntryType.SystemLog,
                _ => TimelineStreamEntryType.SystemLog,
            },
            entry.AuthorId,
            entry.AuthorName,
            entry.Body,
            [],
            entry.ParentEntryId);

        return TypedResults.Created($"/api/timeline/{entityType}/{entityId}/entries/{entry.Id}", result);
    }

#pragma warning disable S1172 // Route parameters bound by ASP.NET Core minimal API
    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteEntryAsync(
        string entityType,
        string entityId,
        Guid entryId,
        [FromServices] ITimelineWriter writer,
        [FromServices] ITimelineReader reader,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
#pragma warning restore S1172
    {
        // VULN-100: Ownership check — only author or admin can delete
        bool isAdmin = await permissionChecker.IsGrantedAsync(TimelinePermissions.Entries.Manage, cancellationToken).ConfigureAwait(false);

        if (!isAdmin)
        {
            PagedResult<TimelineStreamEntry> stream = await reader.GetStreamAsync(entityType, entityId, 1, 1000, cancellationToken).ConfigureAwait(false);
            TimelineStreamEntry? target = stream.Items.FirstOrDefault(e => e.Id == entryId);

            if (target is null)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
            }

            string userId = currentUser.UserId ?? string.Empty;
            if (target.AuthorId != userId)
            {
                return TypedResults.Problem(
                    detail: "You can only delete your own timeline entries.",
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        try
        {
            await writer.DeleteEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
    }
}
