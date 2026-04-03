using Granit.Modularity;
using Granit.Notifications;

namespace Granit.Invoicing.Notifications;

/// <summary>
/// Notification types for invoicing lifecycle events: invoice issued,
/// overdue reminders, payment received, and credit note issued.
/// </summary>
[DependsOn(
    typeof(GranitInvoicingModule),
    typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitInvoicingNotificationsModule : GranitModule;
