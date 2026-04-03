namespace Granit.Payments.Domain;

/// <summary>Payment transaction lifecycle status.</summary>
public enum PaymentStatus
{
    /// <summary>Payment recorded, awaiting provider action.</summary>
    Created = 0,

    /// <summary>Customer must complete an action (3DS, hosted page redirect).</summary>
    RequiresAction = 1,

    /// <summary>Provider is processing (async bank transfer, SEPA).</summary>
    Processing = 2,

    /// <summary>Funds captured successfully.</summary>
    Succeeded = 3,

    /// <summary>Payment declined or errored.</summary>
    Failed = 4,

    /// <summary>Payment cancelled by system or user.</summary>
    Canceled = 5,
}
