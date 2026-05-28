using Granit.Authorization;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.PuppeteerSharp.Extensions;
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

namespace Granit.Browsing.PuppeteerSharp.Tests;

public sealed class PuppeteerScopeValidationTests
{
    [Fact]
    public void AddGranitBrowsingPuppeteerSharp_should_pass_scope_validation_with_scoped_local_event_bus()
    {
        // Regression: PuppeteerHeadlessBrowser is a singleton (owns the browser pool)
        // and previously captured the scoped ILocalEventBus directly — failing
        // ValidateScopes (default in Development) and crashing host startup. The fix
        // routes events through a singleton-safe wrapper backed by IServiceScopeFactory.
        ServiceProvider provider = BuildProvider(addAuthorization: false, addEventBus: true);

        // Resolving via a scope mirrors how ASP.NET Core uses the container at request time.
        using IServiceScope scope = provider.CreateScope();
        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IHeadlessBrowser>());
    }

    [Fact]
    public void AddGranitBrowsingPuppeteerSharp_should_pass_scope_validation_with_scoped_permission_checker()
    {
        // Regression: PuppeteerPdfViewerCapability is a singleton (TryAddSingleton)
        // and previously captured the scoped IPermissionChecker directly — failing
        // ValidateScopes at startup as soon as Granit.Authorization was wired into the
        // host. The fix probes once at construction and routes checks through a
        // singleton-safe wrapper backed by IServiceScopeFactory.
        ServiceProvider provider = BuildProvider(addAuthorization: true, addEventBus: false);

        using IServiceScope scope = provider.CreateScope();
        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IPdfViewerCapability>());
        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IHeadlessBrowser>());
    }

    private static ServiceProvider BuildProvider(bool addAuthorization, bool addEventBus)
    {
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

        if (addEventBus)
        {
            services.TryAddScoped(_ => Substitute.For<ILocalEventBus>());
        }
        if (addAuthorization)
        {
            services.TryAddScoped(_ => Substitute.For<IPermissionChecker>());
        }

        services.AddGranitBrowsingPuppeteerSharp(
            configurePuppeteer: opts => opts.SkipChromiumDownload = true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
