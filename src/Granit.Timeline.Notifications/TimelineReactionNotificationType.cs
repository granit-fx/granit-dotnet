using Granit.Notifications;

namespace Granit.Timeline.Notifications;

/// <summary>
/// Notification type for a reaction added to a timeline entry.
/// Fired only when a reaction is added (not removed, to avoid noise).
/// </summary>
public sealed class TimelineReactionNotificationType
    : NotificationType<TimelineReactionNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TimelineReactionNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "timeline.reaction_toggled";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.InApp, NotificationChannels.SignalR];
}

/// <summary>
/// Data payload for a timeline reaction notification.
/// </summary>
/// <param name="EntityType">The entity type (e.g. "Patient").</param>
/// <param name="EntityId">The entity identifier.</param>
/// <param name="EntryId">The timeline entry identifier.</param>
/// <param name="ReactingUserId">The user who reacted.</param>
/// <param name="ReactingUserName">Display name of the reacting user.</param>
/// <param name="Emoji">Unicode emoji sequence (e.g. <c>"👍"</c>, <c>"❤️"</c>).</param>
public sealed record TimelineReactionNotificationData(
    string EntityType,
    string EntityId,
    Guid EntryId,
    string ReactingUserId,
    string? ReactingUserName,
    string Emoji);
