using Granit.Invoicing.Events;
using Granit.Payments.Wolverine.Internal;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// Delegates to <see cref="AutoChargeService"/> for all charge orchestration logic.
/// </summary>
[global::Wolverine.Attributes.WolverineHandler]
public static class AutoChargeOnInvoiceHandler
{
    public static Task HandleAsync(
        InvoiceFinalizedEto eto,
        AutoChargeService autoChargeService,
        CancellationToken cancellationToken) =>
        autoChargeService.HandleAsync(eto, cancellationToken);
}
