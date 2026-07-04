namespace Granit.Domain.ValueObjects;

/// <summary>
/// Deliverability / existence of an address — the "is it real?" signal, independent of
/// <see cref="AddressGeocodingStatus"/>. A rural address can be <see cref="AddressGeocodingStatus.Failed"/>
/// (a gap in OpenStreetMap) yet <see cref="DeliveryConfirmed"/> because mail actually arrived there.
/// </summary>
public enum AddressVerificationStatus
{
    /// <summary>No verification evidence yet.</summary>
    Unverified,

    /// <summary>Confirmed by an authoritative verification provider (DPV / RDI).</summary>
    ProviderVerified,

    /// <summary>Confirmed by a provider, which standardized / corrected the input.</summary>
    Corrected,

    /// <summary>Confirmed manually by an operator.</summary>
    ManuallyConfirmed,

    /// <summary>Confirmed by a real-world positive outcome (successful courier delivery or postal mail).</summary>
    DeliveryConfirmed,

    /// <summary>Known bad — a provider rejected it, or a delivery / mailing came back undeliverable.</summary>
    Invalid,
}
