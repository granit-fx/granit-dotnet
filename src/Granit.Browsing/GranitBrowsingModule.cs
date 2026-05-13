using Granit.Authorization;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Pages;
using Granit.Browsing.Sandbox;
using Granit.Diagnostics;
using Granit.Guids;
using Granit.Http.Security;
using Granit.IO;
using Granit.Modularity;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing;

/// <summary>
/// Granit module for the headless-browser abstraction layer.
/// </summary>
/// <remarks>
/// Registers <see cref="BrowsingMetrics"/>, the <c>Granit.Browsing</c>
/// <see cref="System.Diagnostics.ActivitySource"/>, the deny-by-default
/// <see cref="IBrowserSandboxProfile"/> (<c>DefaultSandboxProfile</c>), and
/// <see cref="IHarScrubber"/>. Concrete providers (<c>Granit.Browsing.PuppeteerSharp</c>,
/// <c>Granit.Browsing.Playwright</c>) register their own <see cref="IHeadlessBrowser"/>
/// + <see cref="IHeadlessBrowserPool"/> + capability bindings.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpSecurityModule),
    typeof(GranitIOModule),
    typeof(GranitTimingModule))]
public sealed class GranitBrowsingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(BrowsingActivitySource.Name);
        context.Services.TryAddSingleton<BrowsingMetrics>();
        context.Services.TryAddSingleton<IBrowserSandboxProfile, DefaultSandboxProfile>();
        context.Services.TryAddSingleton<IHarScrubber, DefaultHarScrubber>();
        context.Services.AddHostedService<BrowsingPermissionAdvisoryService>();
    }
}

/// <summary>
/// Emits a single Information-level message at startup when no
/// <see cref="IPermissionChecker"/> is registered — clarifying that
/// <c>BrowsingPermissions</c> are advisory in that case (the framework will not enforce
/// them and apps must wire their own authorization layer).
/// </summary>
internal sealed partial class BrowsingPermissionAdvisoryService(
    IServiceProvider services,
    ILogger<BrowsingPermissionAdvisoryService> logger) : IHostedService
{
    /// <inheritdoc/>
    public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken)
    {
        var checker = services.GetService(typeof(IPermissionChecker)) as IPermissionChecker;
        if (checker is null)
        {
            LogAdvisory();
        }
        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken) =>
        System.Threading.Tasks.Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit.Browsing permissions are advisory: no IPermissionChecker is registered. Wire Granit.Authorization to enforce BrowsingPermissions.Pages.* checks.")]
    private partial void LogAdvisory();
}
