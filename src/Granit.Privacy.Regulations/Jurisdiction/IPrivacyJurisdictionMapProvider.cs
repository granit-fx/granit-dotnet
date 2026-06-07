namespace Granit.Privacy.Regulations.Jurisdiction;

/// <summary>
/// Extension point for contributing ISO 3166 → regulation code mappings to <see cref="IPrivacyJurisdictionResolver"/>.
/// Register additional providers via <see cref="GranitPrivacyRegulationsBuilder.AddJurisdictionMapProvider"/>.
/// </summary>
public interface IPrivacyJurisdictionMapProvider
{
    /// <summary>
    /// Populates <paramref name="countryMap"/> (ISO 3166-1 alpha-2 → regulations)
    /// and <paramref name="regionMap"/> (ISO 3166-2 → regulations).
    /// Region entries take precedence over country entries in the resolver.
    /// </summary>
    void Define(
        IDictionary<string, IReadOnlyList<PrivacyRegulation>> countryMap,
        IDictionary<string, IReadOnlyList<PrivacyRegulation>> regionMap);
}
