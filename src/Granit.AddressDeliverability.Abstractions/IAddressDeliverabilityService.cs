using Granit.Domain.ValueObjects;

namespace Granit.AddressDeliverability;

/// <summary>
/// Checks that an <see cref="Address"/> is real and deliverable against an authoritative source (e.g. a
/// postal-authority-backed provider — Smarty, Loqate, Google Address Validation). Tier-1 of the address
/// platform: stronger than geocoding plausibility, weaker than real-world delivery evidence.
/// </summary>
/// <remarks>
/// No concrete provider ships in the framework baseline; a host opts in by registering a provider package.
/// This contract lets the enrichment orchestrator and consumers depend on deliverability without a provider.
/// </remarks>
public interface IAddressDeliverabilityService
{
    /// <summary>Checks <paramref name="address"/> against the configured provider.</summary>
    /// <param name="address">The address to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deliverability result.</returns>
    Task<AddressDeliverabilityResult> CheckAsync(Address address, CancellationToken cancellationToken = default);
}
