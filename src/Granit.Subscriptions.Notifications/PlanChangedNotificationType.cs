using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent when a subscription plan is upgraded or downgraded.</summary>
public sealed class PlanChangedNotificationType
    : NotificationType<PlanChangedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PlanChangedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.PlanChanged";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the plan changed notification.</summary>
public sealed record PlanChangedNotificationData(
    Guid SubscriptionId,
    Guid PreviousPlanId,
    string PreviousPlanName,
    Guid NewPlanId,
    string NewPlanName);
