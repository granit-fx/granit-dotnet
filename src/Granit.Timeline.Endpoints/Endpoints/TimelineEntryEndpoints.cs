using Granit.Authorization;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Timeline.Exceptions;
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

        group.MapPost("/{entityType}/{entityId}/anchor", AnchorExternalAsync)
            .RequireAuthorization(TimelinePermissions.Entries.Create)
            .WithName("AnchorExternalTimelineEntry")
            .WithSummary("Materializes (or returns) the shadow row for an external timeline source entry.")
            .WithDescription("Idempotent: returns the deterministic v5 GUID of the native anchor row so subsequent reactions/replies can target a stable id. The endpoint snapshots the source's body/author/occurred-at at anchor time; if the source later evolves, the shadow keeps its snapshot (audit retention independence).")
            .Produces<AnchorTimelineEntryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{entityType}/{entityId}/entries/{entryId:guid}", UpdateEntryBodyAsync)
            .RequireAuthorization(TimelinePermissions.Entries.Create)
            .WithName("UpdateTimelineEntryBody")
            .WithSummary("Edits the body of an entry the caller authored within the edit window.")
            .WithDescription("Replaces the Markdown body of a Comment or InternalNote authored by the current user, provided the configured edit window (TimelineOptions.EditWindow, default 15 min) has not elapsed. Origin must be Native; SystemLog and external-origin entries are immutable. Returns 403 with reason in extensions when a gate rejects the request.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

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

        // Parse @mentions with cap — send one-time notification only, no auto-follow.
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

    private static async Task<Results<Ok<AnchorTimelineEntryResponse>, ProblemHttpResult>> AnchorExternalAsync(
        string entityType,
        string entityId,
        AnchorTimelineEntryRequest request,
        [FromServices] ITimelineWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid entryId = await writer
                .AnchorExternalAsync(entityType, entityId, request.SourceKey, request.SourceId, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(new AnchorTimelineEntryResponse(entryId));
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
    }

#pragma warning disable S1172 // Route parameters bound by ASP.NET Core minimal API
    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateEntryBodyAsync(
        string entityType,
        string entityId,
        Guid entryId,
        UpdateTimelineEntryBodyRequest request,
        [FromServices] ITimelineWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.UpdateEntryBodyAsync(entryId, request.Body, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        catch (TimelineEntryNotEditableException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                type: "timeline-entry-not-editable",
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reason"] = ex.Reason.ToString(),
                });
        }
    }

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
        // Ownership check — only author or admin can delete.
        bool isAdmin = await permissionChecker.IsGrantedAsync(TimelinePermissions.Entries.Manage, cancellationToken).ConfigureAwait(false);

        if (!isAdmin)
        {
            TimelineStreamResult stream = await reader.GetStreamAsync(entityType, entityId, 1, 1000, cancellationToken).ConfigureAwait(false);
            TimelineStreamEntry? target = stream.Page.Items.FirstOrDefault(e => e.Id == entryId);

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
