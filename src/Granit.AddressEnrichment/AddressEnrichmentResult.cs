using Granit.Domain.ValueObjects;

namespace Granit.AddressEnrichment;

/// <summary>
/// The materialized enrichment of an address: its geocoding outcome and its verification verdict — the two
/// derived axes an address-holding entity stores alongside the postal <see cref="Address"/>.
/// </summary>
/// <param name="Geocoding">The geocoding outcome (coordinate + precision).</param>
/// <param name="Verification">The deliverability verdict.</param>
public sealed record AddressEnrichmentResult(AddressGeocoding Geocoding, AddressVerification Verification);
