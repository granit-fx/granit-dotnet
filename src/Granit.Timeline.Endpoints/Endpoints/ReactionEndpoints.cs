using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Timeline.Events;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace Granit.Timeline.Endpoints.Endpoints;

/// <summary>
/// POST /api/timeline/entries/{entryId}/reactions/{emoji} (toggle) and
/// GET /api/timeline/reactions/catalog (closed list of supported emojis).
/// Both gated by <see cref="TimelinePermissions.Reactions.React"/>.
/// </summary>
internal static class ReactionEndpoints
{
    internal static RouteGroupBuilder MapReactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reactions/catalog", GetCatalogAsync)
            .RequireAuthorization(TimelinePermissions.Reactions.React)
            .WithName("GetTimelineReactionCatalog")
            .WithSummary("Returns the closed catalog of supported reaction emojis.")
            .WithDescription("Returns the 5 v1 emojis (👍 / ❤️ / 🎉 / 😂 / 👀) with their localized display labels for the current request culture. Used by the React shell to render the reaction picker. Permission gate matches the toggle endpoint — a user who cannot react does not need to see the picker.")
            .Produces<IReadOnlyList<ReactionCatalogEntryResponse>>();

        group.MapPost("/entries/{entryId:guid}/reactions/{emoji}", ToggleAsync)
            .RequireAuthorization(TimelinePermissions.Reactions.React)
            .WithName("ToggleTimelineReaction")
            .WithSummary("Toggles a reaction on a timeline entry by the calling user.")
            .WithDescription("Idempotent toggle — adds the reaction if absent, removes if present. Validates the emoji against the closed ReactionEmojiCatalog (returns 400 with Granit:Timeline:UnknownEmoji on a miss). Emits ReactionToggledEvent on the local bus on success. Concurrent double-POST is collapsed by the unique (EntryId, UserId, Emoji) DB index.")
            .Produces<ReactionToggleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static Ok<IReadOnlyList<ReactionCatalogEntryResponse>> GetCatalogAsync(
        [FromServices] IStringLocalizerFactory localizerFactory)
    {
        IStringLocalizer localizer = localizerFactory.Create(typeof(TimelineEndpointsLocalizationResource));

        IReadOnlyList<ReactionCatalogEntryResponse> entries = [.. ReactionEmojiCatalog.All
            .Select(emoji =>
            {
                string key = $"Reaction:{emoji}";
                return new ReactionCatalogEntryResponse(
                    Emoji: emoji,
                    DisplayKey: key,
                    Display: localizer[key].Value);
            })];

        return TypedResults.Ok(entries);
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
        if (!ReactionEmojiCatalog.IsValid(emoji))
        {
            return TypedResults.Problem(
                detail: $"Emoji '{emoji}' is not in the closed catalog (Granit:Timeline:UnknownEmoji).",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown emoji");
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
