using Granit.Invoicing.Abstractions.Events;

namespace Granit.Invoicing;

/// <summary>
/// Strategy for processing an invoice before payment collection.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation (<c>PassThroughPrePaymentProcessor</c> in
/// <c>Granit.Payments</c>) returns the full invoice total unchanged.
/// </para>
/// <para>
/// When <c>Granit.CustomerBalance</c> is loaded, it replaces the default
/// via <c>services.Replace()</c> to deduct available credit before the PSP charge.
/// </para>
/// </remarks>
public interface IInvoicePrePaymentProcessor
{
    /// <summary>
    /// Processes the finalized invoice and returns the remaining amount to charge via PSP.
    /// </summary>
    /// <param name="eto">The invoice finalized event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The remaining amount after any pre-payment deductions.</returns>
    Task<PrePaymentResult> ProcessAsync(InvoiceFinalizedEto eto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pre-payment processing.
/// </summary>
/// <param name="RemainingAmount">Amount remaining to charge via PSP after deductions.</param>
public sealed record PrePaymentResult(decimal RemainingAmount);
