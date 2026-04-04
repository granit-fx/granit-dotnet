using Granit.Domain;

namespace Granit.Payments.SepaDirectDebit.Domain;

/// <summary>A direct debit collection attempt against a mandate.</summary>
public sealed class DirectDebitPayment : Entity
{
    private DirectDebitPayment() { }

    /// <summary>Creates a new collection in Pending status.</summary>
    public static DirectDebitPayment Create(
        Guid id, Guid invoiceId, decimal amount, string currency,
        DateTimeOffset scheduledDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new DirectDebitPayment
        {
            Id = id,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency,
            ScheduledDate = scheduledDate,
            Status = CollectionStatus.Pending,
        };
    }

    /// <summary>Linked invoice ID.</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Collection amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Current status.</summary>
    public CollectionStatus Status { get; private set; }

    /// <summary>Requested collection date.</summary>
    public DateTimeOffset ScheduledDate { get; private set; }

    /// <summary>When the PAIN.008 was sent or API call was made.</summary>
    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>When funds were settled.</summary>
    public DateTimeOffset? SettledAt { get; private set; }

    /// <summary>R-transaction failure code (MD01, AM04, etc.).</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Human-readable failure reason.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>External provider collection ID.</summary>
    public string? ProviderCollectionId { get; private set; }

    /// <summary>Marks as submitted.</summary>
    internal void MarkSubmitted(DateTimeOffset submittedAt, string? providerCollectionId = null)
    {
        Status = CollectionStatus.Submitted;
        SubmittedAt = submittedAt;
        ProviderCollectionId = providerCollectionId;
    }

    /// <summary>Marks as succeeded.</summary>
    internal void MarkSucceeded(DateTimeOffset settledAt)
    {
        Status = CollectionStatus.Succeeded;
        SettledAt = settledAt;
    }

    /// <summary>Marks as failed (R-transaction).</summary>
    internal void MarkFailed(string failureCode, string? failureReason)
    {
        Status = CollectionStatus.Failed;
        FailureCode = failureCode;
        FailureReason = failureReason;
    }

    /// <summary>Marks as refunded.</summary>
    internal void MarkRefunded() => Status = CollectionStatus.Refunded;
}
