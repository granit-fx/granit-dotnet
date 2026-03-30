using System.Collections.Frozen;

namespace Granit.Privacy.Regulations.Profiles.Internal;

/// <summary>
/// Singleton registry built at startup from all <see cref="IRegulationProfileProvider"/> implementations.
/// </summary>
internal sealed class RegulationProfileRegistry : IRegulationProfileRegistry
{
    private readonly FrozenDictionary<string, PrivacyRegulationProfile> _profiles;
    private readonly IReadOnlyList<PrivacyRegulationProfile> _all;

    internal RegulationProfileRegistry(FrozenDictionary<string, PrivacyRegulationProfile> profiles)
    {
        _profiles = profiles;
        _all = [.. profiles.Values];
    }

    public PrivacyRegulationProfile? GetProfile(PrivacyRegulation regulation)
    {
        ArgumentNullException.ThrowIfNull(regulation);
        return _profiles.TryGetValue(regulation.Value, out PrivacyRegulationProfile? profile) ? profile : null;
    }

    public IReadOnlyList<PrivacyRegulationProfile> GetAll() => _all;
}
