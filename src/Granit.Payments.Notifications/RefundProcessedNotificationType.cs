using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>Sent when a refund is successfully processed.</summary>
public sealed class RefundProcessedNotificationType
    : NotificationType<RefundProcessedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly RefundProcessedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Payments.RefundProcessed";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Success;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the refund processed notification.</summary>
public sealed record RefundProcessedNotificationData(
    Guid TransactionId,
    Guid RefundId,
    decimal Amount,
    string Currency,
    string? Reason);
