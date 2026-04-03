using Granit.Notifications;

namespace Granit.Invoicing.Notifications;

/// <summary>Sent when an invoice is past its due date and still unpaid.</summary>
public sealed class InvoiceOverdueNotificationType
    : NotificationType<InvoiceOverdueNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly InvoiceOverdueNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Invoicing.InvoiceOverdue";

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the invoice overdue notification.</summary>
public sealed record InvoiceOverdueNotificationData(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal AmountRemaining,
    string Currency,
    DateTimeOffset DueAt,
    int DaysOverdue);
