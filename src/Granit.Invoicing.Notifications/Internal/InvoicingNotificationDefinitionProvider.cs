using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Invoicing.Notifications.Internal;

internal sealed class InvoicingNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Invoicing";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(InvoiceIssuedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Invoice Issued",
            Description = "Notification when a new invoice is finalized and issued.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(InvoiceOverdueNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Invoice Overdue",
            Description = "Warning when an invoice is past due and unpaid.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(InvoicePaidNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Invoice Paid",
            Description = "Confirmation when an invoice is fully paid.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(CreditNoteIssuedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Credit Note Issued",
            Description = "Notification when a credit note is issued against an invoice.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = true,
        });
    }
}
