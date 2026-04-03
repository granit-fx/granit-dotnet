using Granit.Notifications;

namespace Granit.Invoicing.Notifications;

/// <summary>Sent when an invoice is finalized and issued.</summary>
public sealed class InvoiceIssuedNotificationType
    : NotificationType<InvoiceIssuedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly InvoiceIssuedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Invoicing.InvoiceIssued";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the invoice issued notification.</summary>
public sealed record InvoiceIssuedNotificationData(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Total,
    string Currency,
    DateTimeOffset IssuedAt,
    DateTimeOffset? DueAt);
