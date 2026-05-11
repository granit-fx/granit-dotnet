using System;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Documents.Renditions.Pdf.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Pdf.Tests;

public sealed class PdfRenditionStartupValidatorTests
{
    [Fact]
    public void Validate_throws_when_browser_missing()
    {
        ServiceCollection services = [];
        IServiceProvider sp = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => new PdfRenditionStartupValidator(sp).Validate())
            .Message.ShouldContain("IHeadlessBrowser");
    }

    [Fact]
    public void Validate_throws_when_capability_flag_missing()
    {
        IHeadlessBrowser browser = Substitute.For<IHeadlessBrowser>();
        browser.EngineName.Returns("firefox-playwright");
        browser.Supports(BrowserCapabilities.PdfViewerNative).Returns(false);

        ServiceCollection services = [];
        services.AddSingleton(browser);
        IServiceProvider sp = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => new PdfRenditionStartupValidator(sp).Validate())
            .Message.ShouldContain("PdfViewerNative");
    }

    [Fact]
    public void Validate_throws_when_capability_service_missing()
    {
        IHeadlessBrowser browser = Substitute.For<IHeadlessBrowser>();
        browser.EngineName.Returns("chromium-playwright");
        browser.Supports(BrowserCapabilities.PdfViewerNative).Returns(true);

        ServiceCollection services = [];
        services.AddSingleton(browser);
        IServiceProvider sp = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => new PdfRenditionStartupValidator(sp).Validate())
            .Message.ShouldContain("IPdfViewerCapability");
    }

    [Fact]
    public void Validate_passes_when_everything_is_wired()
    {
        IHeadlessBrowser browser = Substitute.For<IHeadlessBrowser>();
        browser.EngineName.Returns("chromium-playwright");
        browser.Supports(BrowserCapabilities.PdfViewerNative).Returns(true);

        ServiceCollection services = [];
        services.AddSingleton(browser);
        services.AddSingleton(Substitute.For<IPdfViewerCapability>());
        IServiceProvider sp = services.BuildServiceProvider();

        Should.NotThrow(() => new PdfRenditionStartupValidator(sp).Validate());
    }
}
