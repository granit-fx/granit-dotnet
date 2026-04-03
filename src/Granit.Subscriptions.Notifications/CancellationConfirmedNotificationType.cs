using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent when a subscription cancellation is confirmed.</summary>
public sealed class CancellationConfirmedNotificationType
    : NotificationType<CancellationConfirmedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly CancellationConfirmedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.CancellationConfirmed";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the cancellation confirmed notification.</summary>
public sealed record CancellationConfirmedNotificationData(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    string? CancellationReason,
    DateTimeOffset CancelledAt);
