using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>Sent when a payment dispute (chargeback) is opened.</summary>
public sealed class DisputeOpenedNotificationType
    : NotificationType<DisputeOpenedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly DisputeOpenedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Payments.DisputeOpened";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Error;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the dispute opened notification.</summary>
public sealed record DisputeOpenedNotificationData(
    Guid TransactionId,
    Guid DisputeId,
    decimal Amount,
    string Currency,
    string Reason);
