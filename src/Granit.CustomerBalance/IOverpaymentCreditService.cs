namespace Granit.CustomerBalance;

/// <summary>
/// Credits overpayment surplus to a tenant's balance account.
/// Creates the account if it does not exist for the (TenantId, Currency) pair.
/// </summary>
public interface IOverpaymentCreditService
{
    /// <summary>
    /// Credits overpayment amount to the tenant's balance account for the given currency.
    /// </summary>
    /// <param name="tenantId">Tenant whose balance account receives the credit.</param>
    /// <param name="currency">ISO 4217 currency code for the balance account.</param>
    /// <param name="amount">Overpayment amount to credit.</param>
    /// <param name="invoiceId">Source invoice that generated the overpayment.</param>
    /// <param name="cancellationToken"></param>
    Task CreditOverpaymentAsync(
        Guid tenantId,
        string currency,
        decimal amount,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
