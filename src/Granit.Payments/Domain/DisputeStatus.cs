namespace Granit.Payments.Domain;

/// <summary>Payment dispute lifecycle status.</summary>
public enum DisputeStatus
{
    /// <summary>Dispute opened by cardholder.</summary>
    Open = 0,

    /// <summary>Dispute resolved in merchant's favor.</summary>
    Won = 1,

    /// <summary>Dispute resolved in cardholder's favor.</summary>
    Lost = 2,

    /// <summary>Dispute closed (withdrawn or expired).</summary>
    Closed = 3,
}
