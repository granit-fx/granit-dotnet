using System.Collections.Frozen;

namespace Granit.Privacy.Regulations.Jurisdiction.Internal;

/// <summary>
/// Default implementation of <see cref="IPrivacyJurisdictionResolver"/>.
/// Built from all registered <see cref="IPrivacyJurisdictionMapProvider"/> instances at startup.
/// Region lookup takes precedence over country-level mapping.
/// Returns empty list for unknown codes — never throws.
/// </summary>
internal sealed class DefaultPrivacyJurisdictionResolver : IPrivacyJurisdictionResolver
{
    private readonly FrozenDictionary<string, IReadOnlyList<PrivacyRegulation>> _countryMap;
    private readonly FrozenDictionary<string, IReadOnlyList<PrivacyRegulation>> _regionMap;

    public DefaultPrivacyJurisdictionResolver(IEnumerable<IPrivacyJurisdictionMapProvider> providers)
    {
        Dictionary<string, IReadOnlyList<PrivacyRegulation>> countryMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<PrivacyRegulation>> regionMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (IPrivacyJurisdictionMapProvider provider in providers)
        {
            provider.Define(countryMap, regionMap);
        }

        _countryMap = countryMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _regionMap = regionMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<PrivacyRegulation>> ResolveAsync(
        string countryCode,
        string? regionCode = null,
        CancellationToken cancellationToken = default)
    {
        // Region takes precedence over country
        if (!string.IsNullOrWhiteSpace(regionCode)
            && _regionMap.TryGetValue(regionCode, out IReadOnlyList<PrivacyRegulation>? regionResult))
        {
            return Task.FromResult(regionResult);
        }

        if (!string.IsNullOrWhiteSpace(countryCode)
            && _countryMap.TryGetValue(countryCode, out IReadOnlyList<PrivacyRegulation>? countryResult))
        {
            return Task.FromResult(countryResult);
        }

        return Task.FromResult<IReadOnlyList<PrivacyRegulation>>([]);
    }
}
