using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Timeline.Events;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Timeline.Endpoints.Endpoints;

/// <summary>
/// POST /api/timeline/entries/{entryId}/reactions/{emoji} — toggle a
/// reaction on a timeline entry. Any well-formed Unicode emoji sequence
/// is accepted; the picker UI lives client-side.
/// Gated by <see cref="TimelinePermissions.Reactions.React"/>.
/// </summary>
internal static class ReactionEndpoints
{
    internal static RouteGroupBuilder MapReactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/entries/{entryId:guid}/reactions/{emoji}", ToggleAsync)
            .RequireAuthorization(TimelinePermissions.Reactions.React)
            .WithName("ToggleTimelineReaction")
            .WithSummary("Toggles a reaction on a timeline entry by the calling user.")
            .WithDescription("Idempotent toggle — adds the reaction if absent, removes if present. Accepts any well-formed Unicode emoji sequence (see EmojiValidator); rejects malformed input with 400 and Granit:Timeline:InvalidEmoji. Emits ReactionToggledEvent on the local bus on success. Concurrent double-POST is collapsed by the unique (EntryId, UserId, Emoji) DB index.")
            .Produces<ReactionToggleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<Results<Ok<ReactionToggleResponse>, ProblemHttpResult>> ToggleAsync(
        [FromRoute] Guid entryId,
        [FromRoute] string emoji,
        [FromServices] IReactionReader reader,
        [FromServices] IReactionWriter writer,
        [FromServices] ILocalEventBus eventBus,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        if (!EmojiValidator.IsValid(emoji))
        {
            return TypedResults.Problem(
                detail: $"Emoji '{emoji}' is not a valid Unicode emoji sequence (Granit:Timeline:InvalidEmoji).",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid emoji");
        }

        if (currentUser.UserId is not { } userIdRaw
            || !Guid.TryParse(userIdRaw, out Guid userId)
            || userId == Guid.Empty)
        {
            return TypedResults.Problem(
                detail: "Reactions require an authenticated user with a GUID identifier.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        Reaction? existing = await reader
            .FindAsync(entryId, userId, emoji, cancellationToken)
            .ConfigureAwait(false);

        ReactionToggleAction action;
        if (existing is null)
        {
            var reaction = Reaction.Create(
                id: guidGenerator.Create(),
                entryId: entryId,
                userId: userId,
                emoji: emoji,
                createdAt: clock.Normalize(clock.Now),
                createdBy: userIdRaw,
                tenantId: tenantId);
            await writer.AddAsync(reaction, cancellationToken).ConfigureAwait(false);
            action = ReactionToggleAction.Added;
        }
        else
        {
            await writer.RemoveAsync(entryId, userId, emoji, cancellationToken).ConfigureAwait(false);
            action = ReactionToggleAction.Removed;
        }

        await eventBus
            .PublishAsync(new ReactionToggledEvent(entryId, userId, emoji, action), cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Reaction> after = await reader
            .GetByEntryAsync(entryId, cancellationToken)
            .ConfigureAwait(false);
        int count = 0;
        bool currentUserHasReacted = false;
        foreach (Reaction r in after)
        {
            if (string.Equals(r.Emoji, emoji, StringComparison.Ordinal))
            {
                count++;
                if (r.UserId == userId)
                {
                    currentUserHasReacted = true;
                }
            }
        }

        return TypedResults.Ok(new ReactionToggleResponse(
            EntryId: entryId,
            Emoji: emoji,
            Count: count,
            CurrentUserHasReacted: currentUserHasReacted));
    }
}
