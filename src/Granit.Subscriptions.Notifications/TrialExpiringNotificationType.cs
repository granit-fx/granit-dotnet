using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent when a trial is about to expire (configurable days before).</summary>
public sealed class TrialExpiringNotificationType
    : NotificationType<TrialExpiringNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TrialExpiringNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.TrialExpiring";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the trial expiring notification.</summary>
public sealed record TrialExpiringNotificationData(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    int DaysRemaining,
    DateTimeOffset TrialEndsAt);
