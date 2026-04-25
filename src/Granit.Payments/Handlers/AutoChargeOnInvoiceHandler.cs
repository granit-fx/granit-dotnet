using Granit.Invoicing.Abstractions.Events;

namespace Granit.Payments.Handlers;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// Delegates to <see cref="IAutoChargeService"/> for all charge orchestration logic.
/// </summary>
public class AutoChargeOnInvoiceHandler
{
    public static Task HandleAsync(
        InvoiceFinalizedEto eto,
        IAutoChargeService autoChargeService,
        CancellationToken cancellationToken) =>
        autoChargeService.HandleAsync(eto, cancellationToken);
}
