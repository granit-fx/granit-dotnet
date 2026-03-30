namespace Granit.Privacy.Regulations.Profiles;

/// <summary>
/// Context for registering <see cref="PrivacyRegulationProfile"/> instances.
/// Used during startup by <see cref="IRegulationProfileProvider"/> implementations.
/// </summary>
public interface IRegulationProfileContext
{
    /// <summary>
    /// Registers a regulation profile. Throws <see cref="InvalidOperationException"/>
    /// if a profile for the same regulation is already registered.
    /// </summary>
    /// <param name="profile">The regulation profile to register.</param>
    void Register(PrivacyRegulationProfile profile);
}
