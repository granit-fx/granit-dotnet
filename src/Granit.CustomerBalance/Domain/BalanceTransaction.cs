using Granit.Domain;

namespace Granit.CustomerBalance.Domain;

/// <summary>
/// An immutable, append-only entry in a <see cref="BalanceAccount"/> ledger.
/// </summary>
/// <remarks>
/// Transactions are never updated or deleted. The <see cref="Amount"/> is always
/// positive — the direction is determined by <see cref="Type"/> (Credit or Debit).
/// </remarks>
public sealed class BalanceTransaction : Entity
{
    private BalanceTransaction() { }

    internal static BalanceTransaction Create(
        Guid id,
        Guid balanceAccountId,
        TransactionType type,
        decimal amount,
        TransactionSource source,
        string reason,
        DateTimeOffset createdAt,
        Guid? referenceId = null,
        string? referenceType = null,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new BalanceTransaction
        {
            Id = id,
            BalanceAccountId = balanceAccountId,
            Type = type,
            Amount = amount,
            Source = source,
            Reason = reason,
            CreatedAt = createdAt,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Owning balance account identifier.</summary>
    public Guid BalanceAccountId { get; private set; }

    /// <summary>Direction of the transaction (Credit or Debit).</summary>
    public TransactionType Type { get; private set; }

    /// <summary>Transaction amount (always positive).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Origin of the transaction.</summary>
    public TransactionSource Source { get; private set; }

    /// <summary>Human-readable description.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional reference to an external document (invoice, refund, etc.).</summary>
    public Guid? ReferenceId { get; private set; }

    /// <summary>Type of the referenced document (e.g., "Invoice", "Refund").</summary>
    public string? ReferenceType { get; private set; }

    /// <summary>Expiration date for promotional credits. <c>null</c> if non-expiring.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>When this transaction was recorded.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Notification side-channel (not part of the ledger semantics): the most
    /// recent UTC instant the daily pre-expiration scan published a
    /// <c>CreditNearExpirationEto</c> for this credit. Used by the scan service
    /// to short-circuit duplicate publications within the same day; the field
    /// is mutated in-place via <see cref="MarkPreExpirationNoticed"/> and does
    /// NOT alter <see cref="Amount"/>, <see cref="Type"/>, or any ledger field.
    /// </summary>
    public DateTimeOffset? LastPreExpirationNoticedAt { get; private set; }

    /// <summary>
    /// Records that a pre-expiration notice has been published for this credit
    /// at <paramref name="now"/>. Idempotent at the day-bucket level — callers
    /// gate on <see cref="LastPreExpirationNoticedAt"/> &lt; today before invoking.
    /// </summary>
    public void MarkPreExpirationNoticed(DateTimeOffset now) =>
        LastPreExpirationNoticedAt = now;
}
