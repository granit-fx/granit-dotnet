using Granit.Privacy.Regulations.Jurisdiction;
using Granit.Privacy.Regulations.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Regulations;

/// <summary>
/// Builder for customizing the <c>Granit.Privacy.Regulations</c> module.
/// Used within <see cref="Extensions.PrivacyRegulationsServiceCollectionExtensions.AddGranitPrivacyRegulations"/>
/// to register custom Tier 3 regulation profile providers and jurisdiction map providers.
/// </summary>
public sealed class GranitPrivacyRegulationsBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection.</summary>
    internal IServiceCollection Services { get; } = services;

    /// <summary>Custom regulation profile providers (Tier 3).</summary>
    internal List<IRegulationProfileProvider> Providers { get; } = [];

    /// <summary>Custom jurisdiction map providers.</summary>
    internal List<IPrivacyJurisdictionMapProvider> JurisdictionMapProviders { get; } = [];

    /// <summary>
    /// Adds a custom regulation profile provider (Tier 3).
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    public GranitPrivacyRegulationsBuilder AddProvider<TProvider>()
        where TProvider : IRegulationProfileProvider, new()
    {
        Providers.Add(new TProvider());
        return this;
    }

    /// <summary>
    /// Adds a custom regulation profile provider instance (Tier 3).
    /// </summary>
    /// <param name="provider">The provider instance.</param>
    public GranitPrivacyRegulationsBuilder AddProvider(IRegulationProfileProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Providers.Add(provider);
        return this;
    }

    /// <summary>
    /// Adds a custom ISO 3166 → regulation mapping provider.
    /// Custom entries take precedence over the built-in map for the same country/region codes.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    public GranitPrivacyRegulationsBuilder AddJurisdictionMapProvider<TProvider>()
        where TProvider : IPrivacyJurisdictionMapProvider, new()
    {
        JurisdictionMapProviders.Add(new TProvider());
        return this;
    }

    /// <summary>
    /// Adds a custom ISO 3166 → regulation mapping provider instance.
    /// </summary>
    /// <param name="provider">The provider instance.</param>
    public GranitPrivacyRegulationsBuilder AddJurisdictionMapProvider(IPrivacyJurisdictionMapProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        JurisdictionMapProviders.Add(provider);
        return this;
    }
}
