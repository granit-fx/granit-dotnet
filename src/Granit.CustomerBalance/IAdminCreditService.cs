using Granit.CustomerBalance.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.CustomerBalance;

/// <summary>
/// Applies admin credits (promotional, manual adjustment) to a contact's balance account.
/// Creates the account if it does not exist for the <c>(PartyId, Currency)</c> pair.
/// </summary>
public interface IAdminCreditService
{
    /// <summary>
    /// Credits the specified amount to the contact's balance account for the given currency.
    /// </summary>
    /// <param name="tenantId">Owning tenant identifier (multi-tenant isolation).</param>
    /// <param name="contactId">Party whose balance account receives the credit.</param>
    /// <param name="amount">Amount to credit.</param>
    /// <param name="currency">ISO 4217 currency code for the balance account.</param>
    /// <param name="source">Origin of the credit (e.g., Promotional, ManualAdjustment).</param>
    /// <param name="reason">Human-readable description of the credit.</param>
    /// <param name="expiresAt">Optional expiration date for the credit.</param>
    /// <param name="cancellationToken"></param>
    Task<BalanceAccount> ApplyAsync(
        Guid tenantId,
        PartyId contactId,
        decimal amount,
        string currency,
        TransactionSource source,
        string reason,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default);
}
