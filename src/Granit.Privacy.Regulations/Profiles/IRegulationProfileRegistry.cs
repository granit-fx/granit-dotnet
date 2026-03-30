namespace Granit.Privacy.Regulations.Profiles;

/// <summary>
/// Singleton registry of all regulation profiles, populated at startup
/// from <see cref="IRegulationProfileProvider"/> implementations.
/// </summary>
public interface IRegulationProfileRegistry
{
    /// <summary>
    /// Returns the profile for the specified regulation, or <c>null</c> if not registered.
    /// </summary>
    /// <param name="regulation">The regulation to look up.</param>
    PrivacyRegulationProfile? GetProfile(PrivacyRegulation regulation);

    /// <summary>Returns all registered regulation profiles.</summary>
    IReadOnlyList<PrivacyRegulationProfile> GetAll();
}
