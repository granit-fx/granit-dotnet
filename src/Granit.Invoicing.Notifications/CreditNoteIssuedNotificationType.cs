using Granit.Notifications;

namespace Granit.Invoicing.Notifications;

/// <summary>Sent when a credit note is issued against an invoice.</summary>
public sealed class CreditNoteIssuedNotificationType
    : NotificationType<CreditNoteIssuedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly CreditNoteIssuedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Invoicing.CreditNoteIssued";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the credit note issued notification.</summary>
public sealed record CreditNoteIssuedNotificationData(
    Guid CreditNoteId,
    string CreditNoteNumber,
    Guid ParentInvoiceId,
    string ParentInvoiceNumber,
    decimal Amount,
    string Currency,
    string Reason);
