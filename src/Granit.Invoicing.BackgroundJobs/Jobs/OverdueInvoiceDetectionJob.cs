using Granit.BackgroundJobs;

namespace Granit.Invoicing.BackgroundJobs.Jobs;

/// <summary>Detects overdue invoices (Open + past DueAt) and publishes InvoiceOverdueEto.</summary>
[RecurringJob("0 */4 * * *", "invoicing-overdue-detection")]
public sealed record OverdueInvoiceDetectionJob : IBackgroundJob;
