using Granit.Browsing.Playwright.Extensions;
using Granit.Modularity;

namespace Granit.Browsing.Playwright;

/// <summary>Granit module for the Microsoft.Playwright implementation of <c>Granit.Browsing</c>.</summary>
[DependsOn(typeof(GranitBrowsingModule))]
public sealed class GranitBrowsingPlaywrightModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitBrowsingPlaywright();
}
