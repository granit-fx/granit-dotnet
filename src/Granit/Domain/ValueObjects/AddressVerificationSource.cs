namespace Granit.Domain.ValueObjects;

/// <summary>
/// Provenance of an <see cref="AddressVerification"/> verdict — which evidence asserted it. Recorded
/// alongside the verdict so a confirmation is attributable (anti-repudiation), never just a bare status.
/// </summary>
public enum AddressVerificationSource
{
    /// <summary>No source (the default for <see cref="AddressVerificationStatus.Unverified"/>).</summary>
    None,

    /// <summary>Inferred from geocoding plausibility — weak (existence, not deliverability).</summary>
    Geocoding,

    /// <summary>An authoritative address-verification provider.</summary>
    VerificationProvider,

    /// <summary>A human operator confirmed it.</summary>
    Manual,

    /// <summary>A successful courier delivery to the address.</summary>
    Delivery,

    /// <summary>A successful postal mailing (non-returned mail).</summary>
    PostalMail,
}
