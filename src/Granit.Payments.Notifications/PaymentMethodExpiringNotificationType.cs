using Granit.Notifications;

namespace Granit.Payments.Notifications;

/// <summary>Sent when a saved payment method is about to expire.</summary>
public sealed class PaymentMethodExpiringNotificationType
    : NotificationType<PaymentMethodExpiringNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PaymentMethodExpiringNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Payments.PaymentMethodExpiring";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the payment method expiring notification.</summary>
public sealed record PaymentMethodExpiringNotificationData(
    Guid PaymentMethodId,
    string DisplayLabel,
    DateTimeOffset ExpiresAt,
    int DaysRemaining);
