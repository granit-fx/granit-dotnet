using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Browsing.PuppeteerSharp;
using Granit.Browsing.PuppeteerSharp.Extensions;
using Granit.Browsing.PuppeteerSharp.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<Granit.Browsing.Diagnostics.BrowsingMetrics>();
        services.AddGranitBrowsingPuppeteerSharp(
            configurePuppeteer: opts => opts.SkipChromiumDownload = true);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IHeadlessBrowser_should_resolve_to_Puppeteer_provider()
    {
        ServiceProvider provider = BuildProvider();
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();

        browser.ShouldBeOfType<PuppeteerHeadlessBrowser>();
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
    public void Pool_singleton_should_be_same_instance_as_browser()
    {
        ServiceProvider provider = BuildProvider();
        IHeadlessBrowser browser = provider.GetRequiredService<IHeadlessBrowser>();
        IHeadlessBrowserPool pool = provider.GetRequiredService<IHeadlessBrowserPool>();
        ReferenceEquals(browser, pool).ShouldBeTrue();
    }
}
