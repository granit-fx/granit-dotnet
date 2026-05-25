using Granit.TextExtraction.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tests;

public sealed class GranitTextExtractionOptionsTests
{
    [Fact]
    public void Defaults_match_documented_contract()
    {
        GranitTextExtractionOptions options = new();

        options.MaxConcurrentExtractions.ShouldBe(Environment.ProcessorCount);
        options.ExtractionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.MaxBodySizeBytes.ShouldBe(100L * 1024 * 1024);
        options.MaxDecompressedBytes.ShouldBe(500L * 1024 * 1024);
        options.MaxZipEntries.ShouldBe(10_000);
        options.MaxExtractedCharLength.ShouldBe(500_000);
        options.MaxImagePixels.ShouldBe(100_000_000L);
        options.ResolveHtmlExternalResources.ShouldBeFalse();
    }

    [Fact]
    public void Section_name_matches_convention() =>
        GranitTextExtractionOptions.SectionName.ShouldBe("TextExtraction");

    [Fact]
    public void Binds_from_configuration_section()
    {
        Dictionary<string, string?> data = new()
        {
            ["TextExtraction:MaxBodySizeBytes"] = "12345",
            ["TextExtraction:MaxExtractedCharLength"] = "777",
            ["TextExtraction:ResolveHtmlExternalResources"] = "true",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddOptions<GranitTextExtractionOptions>()
            .Bind(configuration.GetSection(GranitTextExtractionOptions.SectionName));

        using ServiceProvider sp = services.BuildServiceProvider();
        GranitTextExtractionOptions bound = sp.GetRequiredService<IOptions<GranitTextExtractionOptions>>().Value;

        bound.MaxBodySizeBytes.ShouldBe(12345);
        bound.MaxExtractedCharLength.ShouldBe(777);
        bound.ResolveHtmlExternalResources.ShouldBeTrue();
        bound.MaxZipEntries.ShouldBe(10_000); // unset → default preserved
    }
}
