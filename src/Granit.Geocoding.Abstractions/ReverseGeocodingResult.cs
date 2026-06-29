using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// The outcome of a reverse-geocoding lookup: the postal address nearest a coordinate plus the match precision.
/// </summary>
/// <remarks>
/// Uses the loose <see cref="PostalAddress"/> (not the strict domain <c>Address</c>) because a reverse result may
/// resolve only to a locality and country — not a full deliverable address. A caller that needs a strict
/// <c>Address</c> builds one when it has enough components.
/// </remarks>
/// <param name="Address">The resolved postal address (at least locality + country).</param>
/// <param name="Precision">Granularity of the match (rooftop / street / locality).</param>
public sealed record ReverseGeocodingResult(PostalAddress Address, GeocodeMatchPrecision Precision);
