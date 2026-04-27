using Granit.Parties.Domain.ValueObjects;

namespace Granit.CustomerBalance;

/// <summary>
/// Credits overpayment surplus to a contact's balance account.
/// Creates the account if it does not exist for the <c>(PartyId, Currency)</c> pair.
/// </summary>
public interface IOverpaymentCreditService
{
    /// <summary>
    /// Credits the overpayment amount to the contact's balance account for the given currency.
    /// </summary>
    /// <param name="tenantId">Owning tenant identifier (multi-tenant isolation).</param>
    /// <param name="contactId">Party whose balance account receives the credit.</param>
    /// <param name="currency">ISO 4217 currency code for the balance account.</param>
    /// <param name="amount">Overpayment amount to credit.</param>
    /// <param name="invoiceId">Source invoice that generated the overpayment.</param>
    /// <param name="cancellationToken"></param>
    Task CreditOverpaymentAsync(
        Guid tenantId,
        PartyId contactId,
        string currency,
        decimal amount,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
