namespace Granit.AddressDeliverability;

/// <summary>
/// The outcome an address-deliverability provider returns for a submitted address. Distinct from the
/// persisted <see cref="Granit.Domain.ValueObjects.AddressVerificationStatus"/>: this is the provider's raw
/// answer, mapped onto the stored verdict by the enrichment orchestrator.
/// </summary>
public enum AddressDeliverabilityOutcome
{
    /// <summary>The address was confirmed deliverable as submitted.</summary>
    Verified = 0,

    /// <summary>The address was confirmed after the provider standardized / corrected it.</summary>
    Corrected = 1,

    /// <summary>The provider could not confirm the address (no authoritative match) — neither valid nor invalid.</summary>
    Unverifiable = 2,

    /// <summary>The provider determined the address is not deliverable.</summary>
    Invalid = 3,
}
