using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>A payment dispute/chargeback (child entity).</summary>
public sealed class Dispute : Entity
{
    private Dispute() { }

    /// <summary>Creates a new open dispute.</summary>
    public static Dispute Create(
        Guid id, string providerDisputeId, string reason,
        decimal amount, string currency, DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            ProviderDisputeId = providerDisputeId,
            Reason = reason,
            Amount = amount,
            Currency = currency,
            Status = DisputeStatus.Open,
            CreatedAt = createdAt,
        };

    public string ProviderDisputeId { get; private set; } = string.Empty;
    public DisputeStatus Status { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Resolves the dispute.</summary>
    internal void Resolve(DisputeStatus outcome, DateTimeOffset resolvedAt)
    {
        Status = outcome;
        ResolvedAt = resolvedAt;
    }
}
