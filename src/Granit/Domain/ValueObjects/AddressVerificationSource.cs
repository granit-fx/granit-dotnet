namespace Granit.Domain.ValueObjects;

/// <summary>
/// Provenance of an <see cref="AddressVerification"/> verdict — which evidence asserted it. Recorded
/// alongside the verdict so a confirmation is attributable (anti-repudiation), never just a bare status.
/// </summary>
public enum AddressVerificationSource
{
    /// <summary>No source (the default for <see cref="AddressVerificationStatus.Unverified"/>).</summary>
    None = 0,

    /// <summary>Inferred from geocoding plausibility — weak (existence, not deliverability).</summary>
    Geocoding = 1,

    /// <summary>An authoritative address-verification provider.</summary>
    VerificationProvider = 2,

    /// <summary>A human operator confirmed it.</summary>
    Manual = 3,

    /// <summary>A successful courier delivery to the address.</summary>
    Delivery = 4,

    /// <summary>A successful postal mailing (non-returned mail).</summary>
    PostalMail = 5,
}
