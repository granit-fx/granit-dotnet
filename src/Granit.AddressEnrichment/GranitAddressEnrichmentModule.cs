using Granit.AddressDeliverability;
using Granit.AddressEnrichment.Internal;
using Granit.Geocoding;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AddressEnrichment;

/// <summary>
/// Granit module for address enrichment — orchestrates geocoding (tier 0) and, when a provider is
/// registered, deliverability (tier 1) into the framework address value objects.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitGeocodingModule"/> so a geocoding service is always available (the no-op
/// default when no provider is installed). Deliverability is an optional soft dependency resolved at
/// registration time: register an <see cref="IAddressDeliverabilityService"/> provider to enable tier 1,
/// otherwise it is skipped.
/// </remarks>
[DependsOn(typeof(GranitGeocodingModule))]
public sealed class GranitAddressEnrichmentModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IAddressEnrichmentService>(serviceProvider =>
            new DefaultAddressEnrichmentService(
                serviceProvider.GetRequiredService<IGeocodingService>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                serviceProvider.GetService<IAddressDeliverabilityService>()));
}
