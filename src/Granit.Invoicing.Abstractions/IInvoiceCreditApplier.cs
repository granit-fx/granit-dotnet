namespace Granit.Invoicing;

/// <summary>
/// Applies a credit note amount to an invoice, handling the domain transition and persistence.
/// </summary>
/// <remarks>
/// <para>
/// Exposed in <c>Granit.Invoicing.Abstractions</c> so modules that need to credit invoices
/// (e.g. <c>Granit.CustomerBalance</c> for balance deductions, gift cards, promotional
/// credits) do not need to reference the full <c>Granit.Invoicing</c> package — they only
/// need the contract.
/// </para>
/// <para>
/// The default implementation in <c>Granit.Invoicing</c> encapsulates <c>IInvoiceReader</c>,
/// <c>IInvoiceWriter</c>, and <see cref="Granit.Invoicing.Domain.Invoice"/> domain methods,
/// including auto-transition to <c>Paid</c> when the remaining amount reaches zero.
/// </para>
/// </remarks>
public interface IInvoiceCreditApplier
{
    /// <summary>
    /// Applies a credit amount to the invoice identified by <paramref name="invoiceId"/>.
    /// </summary>
    /// <remarks>
    /// No-op if the invoice does not exist or is already Paid. Throws
    /// <see cref="InvalidOperationException"/> for invalid status transitions.
    /// </remarks>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="amount">Credit amount to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyCreditAsync(Guid invoiceId, decimal amount, CancellationToken cancellationToken = default);
}
