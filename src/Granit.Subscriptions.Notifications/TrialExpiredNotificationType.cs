using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent when a trial has expired and the subscription transitions to Expired.</summary>
public sealed class TrialExpiredNotificationType
    : NotificationType<TrialExpiredNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TrialExpiredNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.TrialExpired";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the trial expired notification.</summary>
public sealed record TrialExpiredNotificationData(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName);
