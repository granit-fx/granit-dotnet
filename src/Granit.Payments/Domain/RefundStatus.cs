namespace Granit.Payments.Domain;

/// <summary>Refund lifecycle status.</summary>
public enum RefundStatus
{
    /// <summary>Refund initiated, awaiting processing.</summary>
    Pending = 0,

    /// <summary>Refund processed successfully.</summary>
    Succeeded = 1,

    /// <summary>Refund failed (bank rejected).</summary>
    Failed = 2,
}
