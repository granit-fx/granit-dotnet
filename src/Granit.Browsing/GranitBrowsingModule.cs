using Granit.Authorization;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Sandbox;
using Granit.Diagnostics;
using Granit.Guids;
using Granit.Http.Security;
using Granit.IO;
using Granit.Modularity;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Browsing;

/// <summary>
/// Granit module for the headless-browser abstraction layer.
/// </summary>
/// <remarks>
/// Registers <see cref="BrowsingMetrics"/>, the <c>Granit.Browsing</c>
/// <see cref="System.Diagnostics.ActivitySource"/>, and the deny-by-default
/// <see cref="IBrowserSandboxProfile"/> (<c>DefaultSandboxProfile</c>). Concrete
/// providers (<c>Granit.Browsing.PuppeteerSharp</c>, <c>Granit.Browsing.Playwright</c>)
/// register their own <see cref="IHeadlessBrowser"/> + <see cref="IHeadlessBrowserPool"/>
/// + capability bindings.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpSecurityModule),
    typeof(GranitIoModule),
    typeof(GranitTimingModule))]
public sealed class GranitBrowsingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(BrowsingActivitySource.Name);
        context.Services.TryAddSingleton<BrowsingMetrics>();
        context.Services.TryAddSingleton<IBrowserSandboxProfile, DefaultSandboxProfile>();
    }
}
