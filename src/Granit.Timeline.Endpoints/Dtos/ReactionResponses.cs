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
/// endpoint (story C3). Omitted when the entry has zero reactions.
/// </summary>
/// <param name="Count">Total number of reactions of this emoji on the entry.</param>
/// <param name="ByCurrentUser">Whether the calling user is among the reactors.</param>
public sealed record ReactionAggregateResponse(int Count, bool ByCurrentUser);
