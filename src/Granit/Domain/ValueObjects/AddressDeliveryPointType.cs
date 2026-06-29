namespace Granit.Domain.ValueObjects;

/// <summary>
/// Kind of delivery point an <see cref="Address"/> represents. Affects deliverability semantics: a
/// <see cref="PoBox"/> can be confirmed by postal mail but never by a courier delivery.
/// </summary>
public enum AddressDeliveryPointType
{
    /// <summary>A regular street address.</summary>
    Street = 0,

    /// <summary>A post-office box — postal mail only, no courier delivery.</summary>
    PoBox = 1,

    /// <summary>Any other delivery point (military, poste restante, …).</summary>
    Other = 2,
}
