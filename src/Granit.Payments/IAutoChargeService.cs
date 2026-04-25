using Granit.Invoicing.Events;

namespace Granit.Payments;

/// <summary>
/// Automatically initiates payment when an invoice is finalized with auto-collection.
/// </summary>
public interface IAutoChargeService
{
    /// <summary>
    /// Processes an invoice finalization event, applying pre-payment adjustments and
    /// initiating a charge via the default payment method when applicable.
    /// </summary>
    Task HandleAsync(InvoiceFinalizedEto eto, CancellationToken cancellationToken = default);
}
