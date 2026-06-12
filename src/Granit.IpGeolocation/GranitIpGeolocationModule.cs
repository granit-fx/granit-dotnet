using Granit.Caching;
using Granit.Diagnostics;
using Granit.IpGeolocation.Diagnostics;
using Granit.IpGeolocation.Internal;
using Granit.IpGeolocation.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.IpGeolocation;

/// <summary>
/// Granit module for IP geolocation (provider-agnostic core).
/// </summary>
/// <remarks>
/// Registers the no-op default resolver (<see cref="IIpGeolocationResolver"/>). With no provider package
/// installed it resolves every address to <c>null</c> without throwing. Add a provider package (e.g.
/// <c>Granit.IpGeolocation.MaxMind</c> for offline <c>.mmdb</c> lookups, or <c>Granit.IpGeolocation.IpInfo</c>
/// for an opt-in third-party API) and list it in <see cref="GranitIpGeolocationOptions.ProviderOrder"/> to
/// enable resolution. Results are cached through <c>Granit.Caching</c> (<c>IFusionCache</c>), so they become
/// cluster-shared automatically when a distributed cache (e.g. <c>Granit.Caching.StackExchangeRedis</c>) is
/// installed.
/// </remarks>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitIpGeolocationModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(IpGeolocationActivitySource.Name);

        context.Services
            .AddOptions<GranitIpGeolocationOptions>()
            .BindConfiguration(GranitIpGeolocationOptions.SectionName);

        context.Services.TryAddSingleton<IpGeolocationMetrics>();
        context.Services.TryAddSingleton<IIpGeolocationResolver, DefaultIpGeolocationResolver>();
    }
}
