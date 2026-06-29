namespace Granit.Domain.ValueObjects;

/// <summary>
/// Lifecycle of an address geocoding attempt — the "can we place it on a map?" signal, distinct from
/// <see cref="AddressVerificationStatus"/> (deliverability). The two axes are independent: an address may
/// be <see cref="Failed"/> here yet <see cref="AddressVerificationStatus.DeliveryConfirmed"/>.
/// </summary>
public enum AddressGeocodingStatus
{
    /// <summary>Address attached, never geocoded — awaiting a first attempt.</summary>
    Pending = 0,

    /// <summary>Resolved to a precise coordinate (rooftop or street-level match).</summary>
    Resolved = 1,

    /// <summary>Resolved, but only to a locality / postcode centroid — coarse.</summary>
    Approximate = 2,

    /// <summary>No match found (likely a typo, or a gap in the provider's coverage). Not the same as invalid.</summary>
    Failed = 3,

    /// <summary>The address changed since the last successful geocoding — to be re-resolved.</summary>
    Stale = 4,
}
