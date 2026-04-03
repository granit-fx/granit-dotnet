using Granit.DataProtection;
using Granit.Domain;
using Granit.Payments.Events;

namespace Granit.Payments.Domain;

/// <summary>
/// Tracks a payment attempt against an invoice.
/// </summary>
/// <remarks>
/// <para>
/// The FSM (Created → RequiresAction → Processing → Succeeded/Failed/Canceled)
/// is driven by provider webhooks and explicit API calls. All behavior methods
/// are idempotent — returning <c>false</c> if already in the target state.
/// </para>
/// <para>
/// Refunds are child entities with an invariant:
/// <c>sum(refunds.Amount) + newRefund.Amount &lt;= transaction.Amount</c>.
/// </para>
/// </remarks>
public sealed class PaymentTransaction : AuditedAggregateRoot, IMultiTenant
{
    private readonly List<Refund> _refunds = [];
    private readonly List<Dispute> _disputes = [];

    private PaymentTransaction() { }

    /// <summary>Creates a new payment transaction in Created status.</summary>
    public static PaymentTransaction Create(
        Guid id,
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string providerName,
        string idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var transaction = new PaymentTransaction
        {
            Id = id,
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency,
            ProviderName = providerName,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Created,
        };

        transaction.AddDomainEvent(new PaymentCreatedEvent(id, invoiceId, tenantId));
        return transaction;
    }

    // ── Properties ─────────────────────────────────────────────────────

    /// <summary>Invoice this payment is for (Guid, no project reference to Invoicing).</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Payment amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Payment provider name (e.g., "stripe", "mollie").</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Transaction ID in the provider system.</summary>
    public string? ProviderTransactionId { get; private set; }

    /// <summary>Payment method used (if saved).</summary>
    public Guid? PaymentMethodId { get; private set; }

    /// <summary>Redirect URL for 3DS/hosted payment page.</summary>
    public string? ActionUrl { get; private set; }

    /// <summary>Idempotency key preventing double charges.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Provider failure code (e.g., "card_declined").</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Human-readable failure message.</summary>
    [SensitiveData]
    public string? FailureMessage { get; private set; }

    /// <summary>When the payment succeeded.</summary>
    public DateTimeOffset? SucceededAt { get; private set; }

    /// <summary>When the payment was canceled.</summary>
    public DateTimeOffset? CanceledAt { get; private set; }

    /// <summary>Refunds on this transaction.</summary>
    public IReadOnlyList<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>Disputes on this transaction.</summary>
    public IReadOnlyList<Dispute> Disputes => _disputes.AsReadOnly();

    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get; set; }

    // ── Status transitions (idempotent) ────────────────────────────────

    /// <summary>Marks as requiring customer action (3DS, redirect).</summary>
    public bool MarkRequiresAction(string actionUrl)
    {
        if (Status == PaymentStatus.RequiresAction)
        {
            return false;
        }

        if (Status != PaymentStatus.Created)
        {
            throw new InvalidOperationException(
                $"Cannot mark transaction '{Id}' as RequiresAction from '{Status}'.");
        }

        ActionUrl = actionUrl;
        Status = PaymentStatus.RequiresAction;
        return true;
    }

    /// <summary>Marks as processing (async payment, e.g., SEPA).</summary>
    public bool MarkProcessing()
    {
        if (Status == PaymentStatus.Processing)
        {
            return false;
        }

        if (Status is not (PaymentStatus.Created or PaymentStatus.RequiresAction))
        {
            throw new InvalidOperationException(
                $"Cannot mark transaction '{Id}' as Processing from '{Status}'.");
        }

        Status = PaymentStatus.Processing;
        return true;
    }

    /// <summary>Marks as succeeded (funds captured).</summary>
    public bool MarkSucceeded(string providerTransactionId, DateTimeOffset succeededAt)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            return false;
        }

        if (Status is not (PaymentStatus.Created or PaymentStatus.RequiresAction or PaymentStatus.Processing))
        {
            throw new InvalidOperationException(
                $"Cannot mark transaction '{Id}' as Succeeded from '{Status}'.");
        }

        ProviderTransactionId = providerTransactionId;
        SucceededAt = succeededAt;
        Status = PaymentStatus.Succeeded;

        AddDistributedEvent(new PaymentSucceededEto(
            Id, InvoiceId, TenantId!.Value, Amount, Currency, succeededAt));
        return true;
    }

    /// <summary>Marks as failed.</summary>
    public bool MarkFailed(string? failureCode, string? failureMessage)
    {
        if (Status == PaymentStatus.Failed)
        {
            return false;
        }

        if (Status is not (PaymentStatus.Created or PaymentStatus.RequiresAction or PaymentStatus.Processing))
        {
            throw new InvalidOperationException(
                $"Cannot mark transaction '{Id}' as Failed from '{Status}'.");
        }

        FailureCode = failureCode;
        FailureMessage = failureMessage;
        Status = PaymentStatus.Failed;

        AddDistributedEvent(new PaymentFailedEto(
            Id, InvoiceId, TenantId!.Value, failureCode, failureMessage));
        return true;
    }

    /// <summary>Cancels the payment.</summary>
    public bool Cancel(DateTimeOffset canceledAt)
    {
        if (Status == PaymentStatus.Canceled)
        {
            return false;
        }

        if (Status is not (PaymentStatus.Created or PaymentStatus.RequiresAction))
        {
            throw new InvalidOperationException(
                $"Cannot cancel transaction '{Id}' from '{Status}'.");
        }

        CanceledAt = canceledAt;
        Status = PaymentStatus.Canceled;
        return true;
    }

    // ── Refunds ────────────────────────────────────────────────────────

    /// <summary>
    /// Requests a refund. Validates that cumulative refunds don't exceed the transaction amount.
    /// </summary>
    public Refund RequestRefund(Guid refundId, decimal amount, DateTimeOffset createdAt, string? reason = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        if (Status != PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException(
                $"Cannot refund transaction '{Id}' in '{Status}' status. Only Succeeded transactions can be refunded.");
        }

        decimal totalRefunded = _refunds
            .Where(r => r.Status != RefundStatus.Failed)
            .Sum(r => r.Amount);

        if (totalRefunded + amount > Amount)
        {
            throw new InvalidOperationException(
                $"Refund of {amount} {Currency} would exceed transaction amount {Amount} {Currency}. Already refunded: {totalRefunded} {Currency}.");
        }

        var refund = Refund.Create(refundId, amount, Currency, createdAt, reason);
        _refunds.Add(refund);
        return refund;
    }

    /// <summary>Records a completed refund and publishes the event.</summary>
    public void CompleteRefund(Guid refundId, string providerRefundId, DateTimeOffset completedAt)
    {
        Refund refund = _refunds.First(r => r.Id == refundId);
        refund.MarkSucceeded(providerRefundId, completedAt);

        AddDistributedEvent(new RefundCompletedEto(
            Id, refundId, InvoiceId, TenantId!.Value, refund.Amount, Currency));
    }

    // ── Disputes ───────────────────────────────────────────────────────

    /// <summary>Records a new dispute.</summary>
    public Dispute OpenDispute(Guid disputeId, string providerDisputeId, string reason, decimal amount, DateTimeOffset createdAt)
    {
        var dispute = Dispute.Create(disputeId, providerDisputeId, reason, amount, Currency, createdAt);
        _disputes.Add(dispute);

        AddDistributedEvent(new DisputeOpenedEto(
            Id, disputeId, TenantId!.Value, amount, reason));
        return dispute;
    }
}
