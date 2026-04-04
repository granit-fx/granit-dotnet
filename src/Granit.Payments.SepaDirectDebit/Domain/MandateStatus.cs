namespace Granit.Payments.SepaDirectDebit.Domain;

/// <summary>SEPA Direct Debit mandate status.</summary>
public enum MandateStatus
{
    /// <summary>Created, awaiting customer signature or provider approval.</summary>
    Pending = 0,

    /// <summary>Signed and active — collections can be submitted.</summary>
    Active = 1,

    /// <summary>Temporarily paused (dispute, failed collection).</summary>
    Suspended = 2,

    /// <summary>Revoked by customer or merchant.</summary>
    Cancelled = 3,

    /// <summary>Mandate setup failed (bank rejection).</summary>
    Failed = 4,

    /// <summary>No collection for 36 months (SEPA automatic expiry rule).</summary>
    Expired = 5,
}
