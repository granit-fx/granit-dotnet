using Granit.Core.Diagnostics;
using Granit.Core.Modularity;
using Granit.EventBus;
using Granit.Guids;
using Granit.Identity;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Granit.Querying;
using Granit.Security;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict;

/// <summary>
/// Granit module for the OpenIddict-based identity provider.
/// Registers OIDC abstractions, service interfaces, and integration event types.
/// </summary>
[DependsOn(
    typeof(GranitEventBusModule),
    typeof(GranitGuidsModule),
    typeof(GranitIdentityModule),
    typeof(GranitQueryingModule),
    typeof(GranitSecurityModule),
    typeof(GranitTimingModule))]
public sealed class GranitOpenIddictModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<OpenIddictMetrics>();
        GranitActivitySourceRegistry.Register(OpenIddictActivitySource.Name);

        context.Services
            .AddOptions<GranitOpenIddictOptions>()
            .BindConfiguration("OpenIddict");

        context.Services
            .AddOptions<GranitOpenIddictClientOptions>()
            .BindConfiguration(GranitOpenIddictClientOptions.SectionName);

        context.Services
            .AddOptions<GranitPasskeyOptions>()
            .BindConfiguration(GranitPasskeyOptions.SectionName);

        context.Services
            .AddOptions<GranitOpenIddictSeedingOptions>()
            .BindConfiguration(GranitOpenIddictSeedingOptions.SectionName);

        context.Services.TryAddScoped<IClaimsDestinationProvider, DefaultClaimsDestinationProvider>();
    }
}
