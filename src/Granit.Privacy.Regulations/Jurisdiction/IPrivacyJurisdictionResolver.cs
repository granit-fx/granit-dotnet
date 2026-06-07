namespace Granit.Privacy.Regulations.Jurisdiction;

/// <summary>
/// Advisory resolver that maps ISO 3166 country/region codes to applicable privacy regulation codes.
/// The result is a suggestion — the tenant administrator confirms or overrides before it is persisted.
/// </summary>
/// <remarks>
/// Sector-specific regulations (HIPAA, FERPA, COPPA) are excluded — they are not universally applicable
/// within a geography and carry a high false-positive risk. Add them manually when required.
/// </remarks>
public interface IPrivacyJurisdictionResolver
{
    /// <summary>
    /// Returns the privacy regulation codes applicable to the given country and optional region.
    /// Region code takes precedence over country-level mapping when both exist.
    /// Returns an empty list for unknown countries or regions.
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code (e.g. <c>"FR"</c>, <c>"CA"</c>).</param>
    /// <param name="regionCode">
    /// Optional ISO 3166-2 subdivision code (e.g. <c>"CA-QC"</c> for Quebec, <c>"US-CA"</c> for California).
    /// When provided, takes precedence over the country-level entry.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PrivacyRegulation>> ResolveAsync(
        string countryCode,
        string? regionCode = null,
        CancellationToken cancellationToken = default);
}
