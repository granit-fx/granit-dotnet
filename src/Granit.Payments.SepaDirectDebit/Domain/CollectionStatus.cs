namespace Granit.Payments.SepaDirectDebit.Domain;

/// <summary>Status of a direct debit collection attempt.</summary>
public enum CollectionStatus
{
    /// <summary>Created, not yet submitted to bank or provider.</summary>
    Pending = 0,

    /// <summary>PAIN.008 sent to bank or API call made to provider.</summary>
    Submitted = 1,

    /// <summary>Bank is processing (D+2 to D+5 settlement cycle).</summary>
    Processing = 2,

    /// <summary>Funds received.</summary>
    Succeeded = 3,

    /// <summary>Bank returned — R-transaction (MD01, AM04, etc.).</summary>
    Failed = 4,

    /// <summary>Merchant-initiated refund.</summary>
    Refunded = 5,
}
