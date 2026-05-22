namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Returned by <c>POST /api/timeline/entries/{entryId}/reactions/{emoji}</c> —
/// the post-toggle state for the (entry, emoji) pair. <c>Emoji</c> is the
/// Unicode sequence as posted by the client.
/// </summary>
public sealed record ReactionToggleResponse(
    Guid EntryId,
    string Emoji,
    int Count,
    bool CurrentUserHasReacted);

/// <summary>
/// Aggregated reaction counts attached to one timeline entry by the stream
/// endpoint (story C3). Omitted when the entry has zero reactions. The
/// dictionary key under which this is stored is the normalized (skin-tone
/// + VS-16 stripped) form — used as the aggregation identity. The
/// <c>DisplayEmoji</c> field carries one fully-qualified RGI variant from
/// the group so renderers (Twemoji, EmojiOne, …) can resolve the
/// canonical glyph filename without re-implementing TR-51's
/// emoji-presentation reconstruction.
/// </summary>
/// <param name="Count">Total number of reactions of this emoji on the entry.</param>
/// <param name="ByCurrentUser">Whether the calling user is among the reactors.</param>
/// <param name="DisplayEmoji">
/// One fully-qualified original variant from the aggregate (e.g. <c>"👍🏽"</c>
/// or <c>"👨‍⚕️"</c>) — the same wire shape <c>POST /reactions/{emoji}</c>
/// accepts. Stable per entry: the oldest reaction in the group wins.
/// </param>
public sealed record ReactionAggregateResponse(int Count, bool ByCurrentUser, string DisplayEmoji);
