using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent when a subscription is suspended due to unpaid invoices.</summary>
public sealed class SuspensionWarningNotificationType
    : NotificationType<SuspensionWarningNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly SuspensionWarningNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.SuspensionWarning";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Error;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the suspension warning notification.</summary>
public sealed record SuspensionWarningNotificationData(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName);
