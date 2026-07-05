using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Office.Tests;

public sealed class PowerPointTextExtractorTests
{
    private const string Pptx =
        "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private static PowerPointTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(MEOptions.Create(options ?? new ExtractionOptions()),
            NullLogger<PowerPointTextExtractor>.Instance);

    [Theory]
    [InlineData(Pptx, true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_pptx(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_text_from_each_slide()
    {
        byte[] pptx = OfficeFixtures.Pptx("First slide", "Second slide", "Third slide");
        await using MemoryStream stream = new(pptx);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Pptx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("First slide");
        result.Content.ShouldContain("Second slide");
        result.Content.ShouldContain("Third slide");
        result.Content.ShouldContain("\n\n");
        result.ExtractorName.ShouldBe(PowerPointTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Truncates_when_slides_exceed_cap()
    {
        byte[] pptx = OfficeFixtures.Pptx(Enumerable.Range(1, 50).Select(i => $"Slide {i}").ToArray());
        await using MemoryStream stream = new(pptx);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Pptx, maxCharLength: 50, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task Too_large_decompressed_returns_skipped_result()
    {
        ExtractionOptions options = new() { MaxDecompressedBytes = 1024 };
        byte[] adversarial = OfficeFixtures.ZipWithAdvertisedSize(advertisedBytes: 64 * 1024);
        await using MemoryStream stream = new(adversarial);

        TextExtractionResult result = await CreateExtractor(options).ExtractAsync(
            stream, Pptx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }
}
