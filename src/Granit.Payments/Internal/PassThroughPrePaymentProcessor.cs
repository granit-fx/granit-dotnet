using Granit.Invoicing;
using Granit.Invoicing.Abstractions.Events;

namespace Granit.Payments.Internal;

/// <summary>
/// Default pre-payment processor that passes through the full invoice total unchanged.
/// Replaced by <c>Granit.CustomerBalance</c> when the module is loaded.
/// </summary>
internal sealed class PassThroughPrePaymentProcessor : IInvoicePrePaymentProcessor
{
    public Task<PrePaymentResult> ProcessAsync(InvoiceFinalizedEto eto, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PrePaymentResult(eto.Total));
}
