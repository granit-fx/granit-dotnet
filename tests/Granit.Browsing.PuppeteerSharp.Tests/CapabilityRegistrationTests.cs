using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Pool;
using Granit.Browsing.PuppeteerSharp.Extensions;
using Granit.Browsing.PuppeteerSharp.Internal;
using Granit.Browsing.Sandbox;
using Granit.Guids;
using Granit.Http.Security;
using Granit.Http.Security.Extensions;
using Granit.IO.Extensions;
using Granit.MultiTenancy;
using Granit.Timing.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Browsing.PuppeteerSharp.Tests;

public sealed class CapabilityRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<BrowsingMetrics>();
        services.AddSingleton<IBrowserSandboxProfile>(new SandboxProfile());
        // The DI container can't reach DefaultUrlSafetyValidator's internal ctor from a
        // foreign test assembly; substitute the validator so registration succeeds.
        services.AddSingleton(Substitute.For<IUrlSafetyValidator>());
        services.AddGranitUrlSafety();
        services.AddGranitTempFiles();
        services.AddSingleton<ICurrentTenant>(new Granit.MultiTenancy.NullTenantContext());
        services.AddSingleton(TimeProvider.System);
        services.AddGranitTiming();
        services.AddSingleton<IGuidGenerator, UuidV7GuidGenerator>();
        services.AddGranitBrowsingPuppeteerSharp(
            configurePuppeteer: opts => opts.SkipChromiumDownload = true);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IHeadlessBrowser_should_resolve_to_tenant_aware_decorator()
    {
        ServiceProvider provider = BuildProvider();
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();

        browser.ShouldBeOfType<TenantAwareHeadlessBrowser>();
        browser.EngineName.ShouldBe("chromium-puppeteer");
    }

    [Fact]
    public void Capabilities_should_match_advertised_set()
    {
        ServiceProvider provider = BuildProvider();
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();

        browser.Supports(BrowserCapabilities.Screenshot).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.PdfGeneration).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.PdfViewerNative).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.AccessibilityTree).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.NetworkInterception).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.TraceRecording).ShouldBeFalse();
        browser.Supports(BrowserCapabilities.HarRecording).ShouldBeFalse();
    }

    [Fact]
    public void Capability_interfaces_should_resolve()
    {
        ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IPdfCapability>().ShouldBeOfType<PuppeteerPdfCapability>();
        provider.GetRequiredService<IPdfViewerCapability>().ShouldBeOfType<PuppeteerPdfViewerCapability>();
        provider.GetRequiredService<IAccessibilityCapability>().ShouldBeOfType<PuppeteerAccessibilityCapability>();
    }

    [Fact]
    public void Pool_should_resolve_to_underlying_puppeteer_browser()
    {
        ServiceProvider provider = BuildProvider();
        IHeadlessBrowserPool pool = provider.GetRequiredService<IHeadlessBrowserPool>();
        pool.ShouldBeOfType<PuppeteerHeadlessBrowser>();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
