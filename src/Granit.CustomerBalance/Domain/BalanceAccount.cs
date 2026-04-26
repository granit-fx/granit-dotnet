using Granit.Contacts.Domain.ValueObjects;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Exceptions;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.CustomerBalance.Domain;

/// <summary>
/// Per-tenant, per-currency credit balance with an append-only transaction ledger.
/// </summary>
/// <remarks>
/// <para>
/// Each tenant can have one <see cref="BalanceAccount"/> per currency (ISO 4217).
/// The <see cref="Balance"/> is a cached running total updated on every
/// <see cref="Credit"/> / <see cref="Debit"/> operation. <see cref="IConcurrencyAware"/>
/// protects against concurrent updates via optimistic concurrency.
/// </para>
/// <para>
/// Transactions are immutable and append-only — they serve as the audit trail (ISO 27001).
/// </para>
/// </remarks>
public sealed class BalanceAccount : AuditedAggregateRoot, IConcurrencyAware, IMultiTenant
{
    private readonly List<BalanceTransaction> _transactions = [];

    private BalanceAccount() { }

    /// <summary>Creates a new balance account for the given contact, tenant and currency.</summary>
    /// <param name="id">Unique account identifier.</param>
    /// <param name="tenantId">Owning tenant identifier (multi-tenant isolation).</param>
    /// <param name="contactId">Identifier of the <c>Granit.Contacts.Contact</c> that owns this balance — required.</param>
    /// <param name="currency">ISO 4217 currency code (e.g., "EUR").</param>
    public static BalanceAccount Create(Guid id, Guid tenantId, ContactId contactId, string currency)
    {
        ArgumentNullException.ThrowIfNull(contactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new BalanceAccount
        {
            Id = id,
            TenantId = tenantId,
            ContactId = contactId,
            Currency = currency.ToUpperInvariant(),
            Balance = 0m,
            ConcurrencyStamp = string.Empty,
        };
    }

    /// <summary>ISO 4217 currency code (e.g., "EUR", "USD").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Cached running total. Protected by <see cref="ConcurrencyStamp"/>.</summary>
    public decimal Balance { get; private set; }

    /// <summary>Append-only transaction ledger.</summary>
    public IReadOnlyList<BalanceTransaction> Transactions => _transactions.AsReadOnly();

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>
    /// Identifier of the <c>Granit.Contacts.Contact</c> that owns this balance. The
    /// module's name finally matches its domain — a tenant can hold many balance accounts,
    /// one per (contact, currency) tuple, so e-commerce tenants run per-buyer balances.
    /// </summary>
    public ContactId ContactId { get; private set; } = null!;

    /// <inheritdoc/>
    public string ConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>
    /// Adds credit to the balance.
    /// </summary>
    /// <param name="amount">Amount to credit (must be positive).</param>
    /// <param name="source">Origin of the credit.</param>
    /// <param name="reason">Human-readable description.</param>
    /// <param name="createdAt">Timestamp of the transaction.</param>
    /// <param name="transactionId">Unique identifier for the transaction.</param>
    /// <param name="referenceId">Optional external document reference.</param>
    /// <param name="referenceType">Type of the referenced document.</param>
    /// <param name="expiresAt">Expiration date for promotional credits.</param>
    public void Credit(
        decimal amount,
        TransactionSource source,
        string reason,
        DateTimeOffset createdAt,
        Guid transactionId,
        Guid? referenceId = null,
        string? referenceType = null,
        DateTimeOffset? expiresAt = null)
    {
        var transaction = BalanceTransaction.Create(
            transactionId,
            Id,
            TransactionType.Credit,
            amount,
            source,
            reason,
            createdAt,
            referenceId,
            referenceType,
            expiresAt);

        _transactions.Add(transaction);
        Balance += amount;

        AddDistributedEvent(new BalanceCreditedEto(
            Id, TenantId!.Value, ContactId.Value, amount, Currency, source));
    }

    /// <summary>
    /// Deducts credit from the balance.
    /// </summary>
    /// <param name="amount">Amount to debit (must be positive).</param>
    /// <param name="source">Origin of the debit.</param>
    /// <param name="reason">Human-readable description.</param>
    /// <param name="createdAt">Timestamp of the transaction.</param>
    /// <param name="transactionId">Unique identifier for the transaction.</param>
    /// <param name="referenceId">Optional external document reference.</param>
    /// <param name="referenceType">Type of the referenced document.</param>
    /// <exception cref="InsufficientBalanceException">Thrown when balance is insufficient.</exception>
    public void Debit(
        decimal amount,
        TransactionSource source,
        string reason,
        DateTimeOffset createdAt,
        Guid transactionId,
        Guid? referenceId = null,
        string? referenceType = null)
    {
        if (Balance < amount)
        {
            throw new InsufficientBalanceException(Id, Currency, Balance, amount);
        }

        var transaction = BalanceTransaction.Create(
            transactionId,
            Id,
            TransactionType.Debit,
            amount,
            source,
            reason,
            createdAt,
            referenceId,
            referenceType);

        _transactions.Add(transaction);
        Balance -= amount;

        AddDistributedEvent(new BalanceDebitedEto(
            Id, TenantId!.Value, ContactId.Value, amount, Currency, referenceId, referenceType));
    }
}
