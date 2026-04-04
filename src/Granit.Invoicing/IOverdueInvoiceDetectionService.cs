namespace Granit.Invoicing;

/// <summary>
/// Detects overdue invoices and publishes integration events for downstream consumers.
/// </summary>
public interface IOverdueInvoiceDetectionService
{
    /// <summary>Scans all open invoices past their due date and publishes overdue events.</summary>
    Task DetectAsync(CancellationToken cancellationToken = default);
}
