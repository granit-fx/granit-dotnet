using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Playwright;
using Granit.Browsing.Playwright.Extensions;
using Granit.Browsing.Playwright.Internal;
using Granit.Browsing.Playwright.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<Granit.Browsing.Diagnostics.BrowsingMetrics>();
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
        // Tracing + HAR remain available everywhere.
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
}
