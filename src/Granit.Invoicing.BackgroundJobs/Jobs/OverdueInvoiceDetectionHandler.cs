using Granit.Invoicing;

namespace Granit.Invoicing.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OverdueInvoiceDetectionJob"/>. Delegates to
/// <see cref="IOverdueInvoiceDetectionService"/> for overdue invoice detection.
/// </summary>
public class OverdueInvoiceDetectionHandler
{
    public static async Task HandleAsync(
        OverdueInvoiceDetectionJob _,
        IOverdueInvoiceDetectionService detectionService,
        CancellationToken cancellationToken) =>
        await detectionService.DetectAsync(cancellationToken).ConfigureAwait(false);
}
