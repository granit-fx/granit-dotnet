namespace Granit.Domain.ValueObjects;

/// <summary>
/// Deliverability / existence of an address — the "is it real?" signal, independent of
/// <see cref="AddressGeocodingStatus"/>. A rural address can be <see cref="AddressGeocodingStatus.Failed"/>
/// (a gap in OpenStreetMap) yet <see cref="DeliveryConfirmed"/> because mail actually arrived there.
/// </summary>
public enum AddressVerificationStatus
{
    /// <summary>No verification evidence yet.</summary>
    Unverified = 0,

    /// <summary>Confirmed by an authoritative verification provider (DPV / RDI).</summary>
    ProviderVerified = 1,

    /// <summary>Confirmed by a provider, which standardized / corrected the input.</summary>
    Corrected = 2,

    /// <summary>Confirmed manually by an operator.</summary>
    ManuallyConfirmed = 3,

    /// <summary>Confirmed by a real-world positive outcome (successful courier delivery or postal mail).</summary>
    DeliveryConfirmed = 4,

    /// <summary>Known bad — a provider rejected it, or a delivery / mailing came back undeliverable.</summary>
    Invalid = 5,
}
