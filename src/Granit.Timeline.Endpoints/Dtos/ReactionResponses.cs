namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Returned by <c>POST /api/timeline/entries/{entryId}/reactions/{emoji}</c> —
/// the post-toggle state for the (entry, emoji) pair.
/// </summary>
public sealed record ReactionToggleResponse(
    Guid EntryId,
    string Emoji,
    int Count,
    bool CurrentUserHasReacted);

/// <summary>One catalog entry returned by <c>GET /api/timeline/reactions/catalog</c>.</summary>
/// <param name="Emoji">Catalog key (e.g. <c>"thumbs_up"</c>).</param>
/// <param name="DisplayKey">i18n key (<c>Reaction:{key}</c>) for the rendered label.</param>
/// <param name="Display">Localized label for the current request culture.</param>
public sealed record ReactionCatalogEntryResponse(
    string Emoji,
    string DisplayKey,
    string Display);

/// <summary>
/// Aggregated reaction counts attached to one timeline entry by the stream
/// endpoint (story C3). Omitted when the entry has zero reactions.
/// </summary>
/// <param name="Count">Total number of reactions of this emoji on the entry.</param>
/// <param name="ByCurrentUser">Whether the calling user is among the reactors.</param>
public sealed record ReactionAggregateResponse(int Count, bool ByCurrentUser);
