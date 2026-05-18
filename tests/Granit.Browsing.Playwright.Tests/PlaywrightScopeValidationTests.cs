using Granit.Browsing.Diagnostics;
using Granit.Browsing.Playwright.Extensions;
using Granit.Browsing.Playwright.Options;
using Granit.Browsing.Sandbox;
using Granit.Events;
using Granit.Guids;
using Granit.Http.Security;
using Granit.IO.Extensions;
using Granit.MultiTenancy;
using Granit.Timing.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;
using IClock = Granit.Timing.IClock;

namespace Granit.Browsing.Playwright.Tests;

public sealed class PlaywrightScopeValidationTests
{
    [Fact]
    public void AddGranitBrowsingPlaywright_should_pass_scope_validation_with_scoped_local_event_bus()
    {
        // Regression: PlaywrightHeadlessBrowser is singleton (owns the browser pool)
        // and previously captured the scoped ILocalEventBus directly — failing
        // ValidateScopes (default in Development). The fix routes events through a
        // singleton-safe wrapper backed by IServiceScopeFactory.
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<BrowsingMetrics>();
        services.AddSingleton<IBrowserSandboxProfile>(new SandboxProfile());
        services.AddSingleton(Substitute.For<IUrlSafetyValidator>());
        services.AddGranitTempFiles();
        services.AddSingleton<ICurrentTenant>(new NullTenantContext());
        services.AddSingleton(TimeProvider.System);
        services.AddGranitTiming();
        services.AddSingleton<IGuidGenerator, UuidV7GuidGenerator>();

        // The scoped registration that previously broke ValidateScopes.
        services.TryAddScoped(_ => Substitute.For<ILocalEventBus>());

        services.AddGranitBrowsingPlaywright(
            configurePlaywright: opts =>
            {
                opts.SkipBrowserInstall = true;
                opts.Engine = BrowserEngine.Chromium;
            });

        Should.NotThrow(() =>
        {
            using ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
