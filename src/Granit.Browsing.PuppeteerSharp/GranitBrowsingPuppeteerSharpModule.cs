using Granit.Browsing.PuppeteerSharp.Extensions;
using Granit.Modularity;

namespace Granit.Browsing.PuppeteerSharp;

/// <summary>
/// Granit module for the PuppeteerSharp implementation of <c>Granit.Browsing</c>.
/// </summary>
[DependsOn(typeof(GranitBrowsingModule))]
public sealed class GranitBrowsingPuppeteerSharpModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitBrowsingPuppeteerSharp();
}
