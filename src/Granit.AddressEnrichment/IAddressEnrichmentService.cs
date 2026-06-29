using Granit.Domain.ValueObjects;

namespace Granit.AddressEnrichment;

/// <summary>
/// Orchestrates address enrichment: geocodes an <see cref="Address"/> (tier 0) and, when an address
/// verification provider is registered, verifies it (tier 1), mapping both onto the framework value objects.
/// </summary>
public interface IAddressEnrichmentService
{
    /// <summary>Enriches <paramref name="address"/> into its geocoding outcome and verification verdict.</summary>
    /// <param name="address">The address to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The geocoding outcome and verification verdict.</returns>
    Task<AddressEnrichmentResult> EnrichAsync(Address address, CancellationToken cancellationToken = default);
}
