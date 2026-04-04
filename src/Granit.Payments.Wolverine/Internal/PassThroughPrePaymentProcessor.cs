using Granit.Invoicing;
using Granit.Invoicing.Events;

namespace Granit.Payments.Wolverine.Internal;

/// <summary>
/// Default pre-payment processor that passes through the full invoice total unchanged.
/// Replaced by <c>Granit.CustomerBalance.Wolverine</c> when the module is loaded.
/// </summary>
internal sealed class PassThroughPrePaymentProcessor : IInvoicePrePaymentProcessor
{
    public Task<PrePaymentResult> ProcessAsync(InvoiceFinalizedEto eto, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrePaymentResult(eto.Total));
}
