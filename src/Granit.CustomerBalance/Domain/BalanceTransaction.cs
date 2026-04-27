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

    /// <summary>
    /// Timestamp of the last "expiration approaching" notification emitted for this credit
    /// — populated only when this transaction is a promotional credit. Used by the
    /// expiration scanner job to dedupe <c>CreditExpiringEto</c> emissions per credit.
    /// </summary>
    public DateTimeOffset? LastExpirationNotifiedAt { get; private set; }

    /// <summary>When this transaction was recorded.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Records that a "credit expiring soon" notification was emitted for this credit at
    /// the given timestamp. Called by the expiration scanner once an
    /// <c>CreditExpiringEto</c> has been published.
    /// </summary>
    /// <param name="notifiedAt">Timestamp of the notification.</param>
    internal void MarkExpirationNotified(DateTimeOffset notifiedAt) =>
        LastExpirationNotifiedAt = notifiedAt;
}
