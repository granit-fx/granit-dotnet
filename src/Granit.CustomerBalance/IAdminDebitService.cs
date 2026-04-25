using Granit.CustomerBalance.Domain;

namespace Granit.CustomerBalance;

/// <summary>
/// Admin-only debit on a tenant's <see cref="BalanceAccount"/>. Used for manual
/// corrections, scheduled drawdowns, or non-invoice adjustments — operations that
/// the standard <c>CustomerBalancePrePaymentProcessor</c> (invoice flow) cannot
/// cover.
/// </summary>
/// <remarks>
/// Idempotency: when <paramref name="referenceId"/> is supplied, a previous
/// <see cref="BalanceTransaction"/> with the same <c>(ReferenceId, Source)</c>
/// pair short-circuits the operation — replaying the same admin action returns
/// the original outcome without double-debiting. The same pattern is used by
/// <c>CustomerBalancePrePaymentProcessor</c>, so the dedup story is uniform.
/// </remarks>
public interface IAdminDebitService
{
    /// <summary>
    /// Debits the supplied amount from the tenant's <see cref="BalanceAccount"/>
    /// for the given currency.
    /// </summary>
    /// <param name="tenantId">Tenant whose balance account is debited.</param>
    /// <param name="amount">Amount to debit (must be positive).</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="reason">Human-readable description (audit trail).</param>
    /// <param name="referenceId">Optional external document reference for idempotency.</param>
    /// <param name="referenceType">Type of the referenced document (e.g. <c>"AdminAdjustment"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated <see cref="BalanceAccount"/>.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when no account exists for the tenant/currency.</exception>
    /// <exception cref="Exceptions.InsufficientBalanceException">Thrown when the balance is insufficient.</exception>
    Task<BalanceAccount> DebitAsync(
        Guid tenantId,
        decimal amount,
        string currency,
        string reason,
        Guid? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default);
}
