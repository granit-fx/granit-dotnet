using Granit.Http.Hosting.ResponseCompression.Internal;
using Granit.Http.Hosting.ResponseCompression.Options;
using Microsoft.AspNetCore.ResponseCompression;
using Shouldly;
using Xunit;

namespace Granit.Http.Hosting.Tests;

public sealed class ConfigureResponseCompressionOptionsTests
{
    private static ResponseCompressionOptions ConfigureWith(
        GranitResponseCompressionOptions granitOptions)
    {
        ConfigureResponseCompressionOptions configurator = new(
            Microsoft.Extensions.Options.Options.Create(granitOptions));

        ResponseCompressionOptions options = new();
        configurator.Configure(options);
        return options;
    }

    [Fact]
    public void Configure_EnablesHttpsByDefault()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions());

        options.EnableForHttps.ShouldBeTrue();
    }

    [Fact]
    public void Configure_DisablesHttpsWhenConfigured()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions { EnableForHttps = false });

        options.EnableForHttps.ShouldBeFalse();
    }

    [Fact]
    public void Configure_IncludesSvgInMimeTypes()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions());

        options.MimeTypes.ShouldContain("image/svg+xml");
    }

    [Fact]
    public void Configure_IncludesDefaultMimeTypes()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions());

        foreach (string mimeType in ResponseCompressionDefaults.MimeTypes)
        {
            options.MimeTypes.ShouldContain(mimeType);
        }
    }

    [Fact]
    public void Configure_ExcludesSseStream()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions());

        options.ExcludedMimeTypes.ShouldContain("text/event-stream");
    }

    [Fact]
    public void Configure_AddsBrotliAndGzipProvidersByDefault()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions());

        options.Providers.Count.ShouldBe(2);
    }

    [Fact]
    public void Configure_AddsOnlyBrotliWhenGzipDisabled()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions { EnableGzip = false });

        options.Providers.Count.ShouldBe(1);
    }

    [Fact]
    public void Configure_AddsOnlyGzipWhenBrotliDisabled()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions { EnableBrotli = false });

        options.Providers.Count.ShouldBe(1);
    }

    [Fact]
    public void Configure_NoProvidersWhenBothDisabled()
    {
        ResponseCompressionOptions options = ConfigureWith(new GranitResponseCompressionOptions
        {
            EnableBrotli = false,
            EnableGzip = false,
        });

        options.Providers.ShouldBeEmpty();
    }
}
