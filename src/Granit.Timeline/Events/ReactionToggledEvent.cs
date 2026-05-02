using Granit.Events;

namespace Granit.Timeline.Events;

/// <summary>
/// Raised on the local event bus by the toggle endpoint (story C2) after a
/// reaction is added or removed. Notification / follower modules subscribe
/// to surface "X reacted with 👍 to your comment" messages without re-querying
/// the database.
/// </summary>
/// <param name="EntryId">The timeline entry the reaction is on.</param>
/// <param name="UserId">The user who toggled.</param>
/// <param name="Emoji">Catalog key (e.g. <c>"thumbs_up"</c>).</param>
/// <param name="Action">Whether the toggle resulted in an add or a remove.</param>
public sealed record ReactionToggledEvent(
    Guid EntryId,
    Guid UserId,
    string Emoji,
    ReactionToggleAction Action) : IDomainEvent;

/// <summary>The two outcomes of a reaction toggle.</summary>
public enum ReactionToggleAction
{
    /// <summary>The reaction was previously absent and has been added.</summary>
    Added = 0,

    /// <summary>The reaction was previously present and has been removed.</summary>
    Removed = 1,
}
