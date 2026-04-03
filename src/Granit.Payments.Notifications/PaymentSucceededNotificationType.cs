using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>Sent when a payment is successfully processed.</summary>
public sealed class PaymentSucceededNotificationType
    : NotificationType<PaymentSucceededNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PaymentSucceededNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Payments.PaymentSucceeded";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Success;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the payment succeeded notification.</summary>
public sealed record PaymentSucceededNotificationData(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string ProviderName,
    DateTimeOffset SucceededAt);
