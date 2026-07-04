namespace Granit.AddressDeliverability;

/// <summary>
/// The outcome an address-deliverability provider returns for a submitted address. Distinct from the
/// persisted <see cref="Granit.Domain.ValueObjects.AddressVerificationStatus"/>: this is the provider's raw
/// answer, mapped onto the stored verdict by the enrichment orchestrator.
/// </summary>
public enum AddressDeliverabilityOutcome
{
    /// <summary>The address was confirmed deliverable as submitted.</summary>
    Verified,

    /// <summary>The address was confirmed after the provider standardized / corrected it.</summary>
    Corrected,

    /// <summary>The provider could not confirm the address (no authoritative match) — neither valid nor invalid.</summary>
    Unverifiable,

    /// <summary>The provider determined the address is not deliverable.</summary>
    Invalid,
}
