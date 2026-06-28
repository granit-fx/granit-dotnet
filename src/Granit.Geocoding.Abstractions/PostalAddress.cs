namespace Granit.Geocoding;

/// <summary>
/// A postal address submitted for forward geocoding.
/// </summary>
/// <remarks>
/// This is deliberately <strong>not</strong> <c>Granit.Domain.ValueObjects.Address</c>: that value object enforces
/// invariants tuned for storing a deliverable mailing address (e.g. a non-empty street line and postal code), which
/// are too strict for geocoding. A geocoder can resolve a coordinate from just a locality and a country
/// (<c>"Brussels", "BE"</c>) with no street at all, so only <see cref="Locality"/> and <see cref="Country"/> are
/// required here and <see cref="Street"/> / <see cref="PostalCode"/> are optional. Keeping a dedicated, looser shape
/// means the geocoding contract never has to relax the domain Address invariants.
/// </remarks>
/// <param name="Street">Street line (number + name), or <c>null</c> when unknown.</param>
/// <param name="PostalCode">Postal/ZIP code, or <c>null</c> when unknown.</param>
/// <param name="Locality">City/town/locality name (e.g. <c>"Brussels"</c>). Required.</param>
/// <param name="Country">Country name or ISO 3166-1 alpha-2 code (e.g. <c>"Belgium"</c> or <c>"BE"</c>). Required.</param>
public sealed record PostalAddress(string? Street, string? PostalCode, string Locality, string Country);
