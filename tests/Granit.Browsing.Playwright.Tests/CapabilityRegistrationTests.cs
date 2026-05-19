using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Playwright.Extensions;
using Granit.Browsing.Playwright.Internal;
using Granit.Browsing.Playwright.Options;
using Granit.Browsing.Pool;
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

namespace Granit.Browsing.Playwright.Tests;

public sealed class CapabilityRegistrationTests
{
    private static ServiceProvider BuildProvider(BrowserEngine engine)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton<BrowsingMetrics>();
        services.AddSingleton<IBrowserSandboxProfile>(new SandboxProfile());
        services.AddSingleton(Substitute.For<IUrlSafetyValidator>());
        services.AddGranitUrlSafety();
        services.AddGranitTempFiles();
        services.AddSingleton<ICurrentTenant>(new NullTenantContext());
        services.AddSingleton(TimeProvider.System);
        services.AddGranitTiming();
        services.AddSingleton<IGuidGenerator, UuidV7GuidGenerator>();
        services.AddGranitBrowsingPlaywright(
            configurePlaywright: opts =>
            {
                opts.SkipBrowserInstall = true;
                opts.Engine = engine;
            });
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(BrowserEngine.Chromium, "chromium-playwright")]
    [InlineData(BrowserEngine.Firefox, "firefox-playwright")]
    [InlineData(BrowserEngine.Webkit, "webkit-playwright")]
    public void EngineName_should_reflect_configured_engine(BrowserEngine engine, string expected)
    {
        ServiceProvider provider = BuildProvider(engine);
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();
        browser.EngineName.ShouldBe(expected);
    }

    [Fact]
    public void IHeadlessBrowser_should_resolve_to_tenant_aware_decorator()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Chromium);
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();

        browser.ShouldBeOfType<TenantAwareHeadlessBrowser>();
    }

    [Fact]
    public void Pool_should_resolve_to_underlying_playwright_browser()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Chromium);
        IHeadlessBrowserPool pool = provider.GetRequiredService<IHeadlessBrowserPool>();
        pool.ShouldBeOfType<PlaywrightHeadlessBrowser>();
    }

    [Fact]
    public void Chromium_engine_should_advertise_pdf_capabilities()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Chromium);
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();
        browser.Supports(BrowserCapabilities.PdfGeneration).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.PdfViewerNative).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.TraceRecording).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.HarRecording).ShouldBeTrue();
    }

    [Theory]
    [InlineData(BrowserEngine.Firefox)]
    [InlineData(BrowserEngine.Webkit)]
    public void Non_chromium_engines_should_not_advertise_pdf(BrowserEngine engine)
    {
        ServiceProvider provider = BuildProvider(engine);
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();
        browser.Supports(BrowserCapabilities.PdfGeneration).ShouldBeFalse();
        browser.Supports(BrowserCapabilities.PdfViewerNative).ShouldBeFalse();
        browser.Supports(BrowserCapabilities.TraceRecording).ShouldBeTrue();
        browser.Supports(BrowserCapabilities.HarRecording).ShouldBeTrue();
    }

    [Fact]
    public void IPdfCapability_resolution_should_throw_for_non_chromium_engine()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Firefox);
        Should.Throw<System.InvalidOperationException>(() => provider.GetRequiredService<IPdfCapability>());
    }

    [Fact]
    public void IPdfViewerCapability_resolution_should_throw_for_non_chromium_engine()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Webkit);
        Should.Throw<System.InvalidOperationException>(() => provider.GetRequiredService<IPdfViewerCapability>());
    }

    [Fact]
    public void Tracing_and_har_capabilities_should_resolve_on_every_engine()
    {
        foreach (BrowserEngine engine in System.Enum.GetValues<BrowserEngine>())
        {
            ServiceProvider provider = BuildProvider(engine);
            provider.GetRequiredService<ITracingCapability>().ShouldBeOfType<PlaywrightTracingCapability>();
            provider.GetRequiredService<IHarRecordingCapability>().ShouldBeOfType<PlaywrightHarRecordingCapability>();
        }
    }

    [Fact]
    public void Accessibility_capability_should_not_be_registered()
    {
        ServiceProvider provider = BuildProvider(BrowserEngine.Chromium);
        provider.GetService<IAccessibilityCapability>().ShouldBeNull();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
