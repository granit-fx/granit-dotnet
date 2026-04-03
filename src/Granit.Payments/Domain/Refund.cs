using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>A refund on a payment transaction (child entity).</summary>
public sealed class Refund : Entity
{
    private Refund() { }

    /// <summary>Creates a new pending refund.</summary>
    public static Refund Create(
        Guid id, decimal amount, string currency,
        DateTimeOffset createdAt, string? reason = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return new Refund
        {
            Id = id,
            Amount = amount,
            Currency = currency,
            Reason = reason,
            Status = RefundStatus.Pending,
            CreatedAt = createdAt,
        };
    }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public RefundStatus Status { get; private set; }
    public string? ProviderRefundId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Marks the refund as succeeded.</summary>
    internal void MarkSucceeded(string providerRefundId, DateTimeOffset completedAt)
    {
        ProviderRefundId = providerRefundId;
        Status = RefundStatus.Succeeded;
        CompletedAt = completedAt;
    }

    /// <summary>Marks the refund as failed.</summary>
    internal void MarkFailed()
    {
        Status = RefundStatus.Failed;
    }
}
