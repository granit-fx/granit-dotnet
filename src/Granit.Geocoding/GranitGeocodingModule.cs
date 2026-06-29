using Granit.Caching;
using Granit.Diagnostics;
using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Internal;
using Granit.Geocoding.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding;

/// <summary>
/// Granit module for forward geocoding (provider-agnostic core).
/// </summary>
/// <remarks>
/// Registers the no-op default service (<see cref="IGeocodingService"/>). With no provider package installed it
/// resolves every address to <c>null</c> without throwing. Add a provider package (e.g.
/// <c>Granit.Geocoding.Nominatim</c>) and list it in <see cref="GranitGeocodingOptions.ProviderOrder"/> to enable
/// resolution. Results are cached through <c>Granit.Caching</c> (<c>IFusionCache</c>) with separate success/failure
/// TTLs, so they become cluster-shared automatically when a distributed cache (e.g.
/// <c>Granit.Caching.StackExchangeRedis</c>) is installed.
/// </remarks>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitGeocodingModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(GeocodingActivitySource.Name);

        context.Services
            .AddOptions<GranitGeocodingOptions>()
            .BindConfiguration(GranitGeocodingOptions.SectionName);

        context.Services.TryAddSingleton<IValidateOptions<GranitGeocodingOptions>, GranitGeocodingOptionsValidator>();

        context.Services.TryAddSingleton<GeocodingMetrics>();
        context.Services.TryAddSingleton<IGeocodingService, DefaultGeocodingService>();
        context.Services.TryAddSingleton<IReverseGeocodingService, DefaultReverseGeocodingService>();
        context.Services.TryAddSingleton<IAddressAutocompleteService, DefaultAddressAutocompleteService>();

        // Computed once from the registered provider set so hosts/endpoints can branch on what is available
        // (e.g. only map an autocomplete endpoint when an autocomplete-capable provider is installed).
        context.Services.TryAddSingleton(serviceProvider => new GeocodingCapabilities(
            Forward: serviceProvider.GetServices<IGeocodingProvider>().Any(),
            Autocomplete: serviceProvider.GetServices<IAddressAutocompleteProvider>().Any(),
            Reverse: serviceProvider.GetServices<IReverseGeocodingProvider>().Any()));
    }
}
