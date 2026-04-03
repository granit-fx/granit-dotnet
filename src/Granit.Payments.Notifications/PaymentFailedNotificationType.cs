using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>Sent when a payment attempt fails.</summary>
public sealed class PaymentFailedNotificationType
    : NotificationType<PaymentFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PaymentFailedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Payments.PaymentFailed";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the payment failed notification.</summary>
public sealed record PaymentFailedNotificationData(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string? FailureCode,
    string ProviderName);
