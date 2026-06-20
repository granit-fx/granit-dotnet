using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Mentions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>Searches the entities the caller may <c>@</c>-mention, backing the composer picker.</summary>
internal static class MentionsEndpoints
{
    /// <summary>The default number of suggestions returned when the caller omits <c>limit</c>.</summary>
    private const int DefaultLimit = 8;

    /// <summary>The hard ceiling on suggestions, mirroring the per-turn mention cap.</summary>
    private const int MaxLimit = 25;

    internal static RouteGroupBuilder MapMentionsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/mentions", SearchMentionsAsync)
            .WithName("SearchChatMentions")
            .WithSummary("Searches mentionable entities for the @ picker.")
            .WithDescription(
                "Returns the entities the caller may @-mention that match the query, resolved under "
                + "the caller's ACLs across the application's opted-in mention resolvers. Optionally "
                + "narrowed to a single type. Entities the caller cannot see never appear.")
            .Produces<MentionSearchResponse>()
            .RequireAuthorization(AIChatPermissions.Conversations.Send);

        return group;
    }

    private static async Task<Ok<MentionSearchResponse>> SearchMentionsAsync(
        [FromServices] IAIMentionSearchService search,
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        int effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        IReadOnlyList<AIMentionSuggestion> suggestions = await search
            .SearchAsync(q ?? string.Empty, type, effectiveLimit, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new MentionSearchResponse(
            [.. suggestions.Select(s => new MentionSuggestionResponse(s.Type, s.Id, s.Label, s.Description))]));
    }
}
