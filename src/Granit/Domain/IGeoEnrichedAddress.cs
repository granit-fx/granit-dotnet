using Granit.Domain.ValueObjects;

namespace Granit.Domain;

/// <summary>
/// An entity that owns a postal <see cref="Address"/> together with its two derived enrichment axes —
/// geocoding (<see cref="AddressGeocoding"/>) and verification (<see cref="AddressVerification"/>).
/// </summary>
/// <remarks>
/// <para>
/// Implement this on any address-holding entity (e.g. a party address, a branch, a shipping address) so
/// <em>entity-agnostic</em> tooling can enrich it uniformly — read the address, apply the result — without
/// knowing the concrete type. The address platform's enrichment helper
/// (<c>Granit.AddressEnrichment</c>'s <c>EnrichAsync(IGeoEnrichedAddress)</c>) operates on this contract,
/// so the read-enrich-write-back step is written once and reused across holders.
/// </para>
/// <para>
/// The contract uses only framework value objects, so an implementer needs no dependency beyond base
/// <c>Granit</c>. <see cref="ApplyEnrichment"/> covers the geocoding/verification-provider flow; real-world
/// confirmation (manual, delivery) is separate behaviour the entity exposes itself.
/// </para>
/// </remarks>
public interface IGeoEnrichedAddress
{
    /// <summary>The postal address to enrich.</summary>
    Address Address { get; }

    /// <summary>The current geocoding outcome.</summary>
    AddressGeocoding Geocoding { get; }

    /// <summary>The current verification verdict.</summary>
    AddressVerification Verification { get; }

    /// <summary>Applies an enrichment result, replacing the geocoding and verification axes.</summary>
    /// <param name="geocoding">The new geocoding outcome.</param>
    /// <param name="verification">The new verification verdict.</param>
    void ApplyEnrichment(AddressGeocoding geocoding, AddressVerification verification);
}
