using Granit.Privacy.Regulations.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Regulations;

/// <summary>
/// Builder for customizing the <c>Granit.Privacy.Regulations</c> module.
/// Used within <see cref="Extensions.PrivacyRegulationsServiceCollectionExtensions.AddGranitPrivacyRegulations"/>
/// to register custom Tier 3 regulation profile providers.
/// </summary>
public sealed class GranitPrivacyRegulationsBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection.</summary>
    internal IServiceCollection Services { get; } = services;

    /// <summary>Custom regulation profile providers (Tier 3).</summary>
    internal List<IRegulationProfileProvider> Providers { get; } = [];

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
}
