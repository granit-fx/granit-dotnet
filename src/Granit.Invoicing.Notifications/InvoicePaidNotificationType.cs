using Granit.Notifications;

namespace Granit.Invoicing.Notifications;

/// <summary>Sent when an invoice is fully paid.</summary>
public sealed class InvoicePaidNotificationType
    : NotificationType<InvoicePaidNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly InvoicePaidNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Invoicing.InvoicePaid";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Success;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the invoice paid notification.</summary>
public sealed record InvoicePaidNotificationData(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Total,
    string Currency,
    DateTimeOffset PaidAt);
