using Granit.Diagnostics;
using Granit.Identity.Local;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Granit.QueryEngine;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict;

/// <summary>
/// Granit module for the OpenIddict-based identity provider.
/// Registers OIDC abstractions, service interfaces, and integration event types.
/// </summary>
[DependsOn(
    typeof(GranitIdentityLocalModule),
    typeof(GranitQueryEngineAbstractionsModule))]
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
        context.Services.TryAddScoped<ITotpService, DefaultTotpService>();
    }
}
