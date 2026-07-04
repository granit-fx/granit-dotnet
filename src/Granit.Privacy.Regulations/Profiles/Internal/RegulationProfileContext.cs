using System.Collections.Frozen;

namespace Granit.Privacy.Regulations.Profiles.Internal;

/// <summary>
/// Mutable context used during startup to collect regulation profiles
/// from <see cref="IRegulationProfileProvider"/> implementations.
/// </summary>
internal sealed class RegulationProfileContext : IRegulationProfileContext
{
    private readonly Dictionary<string, PrivacyRegulationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public void Register(PrivacyRegulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.Regulation);

        string key = profile.Regulation.Value;

        if (!_profiles.TryAdd(key, profile))
        {
            throw new InvalidOperationException(
                $"A regulation profile for '{key}' is already registered. " +
                "Each regulation can only have one profile.");
        }
    }

    /// <summary>Returns the collected profiles as a frozen dictionary for the registry.</summary>
    internal FrozenDictionary<string, PrivacyRegulationProfile> Build() =>
        _profiles.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
