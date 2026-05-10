using Granit.Browsing.Diagnostics;
using Granit.Diagnostics;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Browsing;

/// <summary>
/// Granit module for the headless-browser abstraction layer.
/// </summary>
/// <remarks>
/// Anchor module. Registers <see cref="BrowsingMetrics"/> and the
/// <c>Granit.Browsing</c> <see cref="System.Diagnostics.ActivitySource"/>; concrete
/// providers (<c>Granit.Browsing.PuppeteerSharp</c>, <c>Granit.Browsing.Playwright</c>)
/// register their own <see cref="IHeadlessBrowser"/> + <see cref="IHeadlessBrowserPool"/>
/// + capability bindings.
/// </remarks>
public sealed class GranitBrowsingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(BrowsingActivitySource.Name);
        context.Services.TryAddSingleton<BrowsingMetrics>();
    }
}
