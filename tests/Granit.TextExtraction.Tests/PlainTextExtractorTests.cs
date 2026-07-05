using System.Text;
using Granit.TextExtraction.Exceptions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Tests;

public sealed class PlainTextExtractorTests
{
    private static PlainTextExtractor CreateExtractor(ExtractionOptions? options = null)
    {
        options ??= new ExtractionOptions();
        return new PlainTextExtractor(MEOptions.Create(options));
    }

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Extracts_full_text_when_under_cap()
    {
        PlainTextExtractor extractor = CreateExtractor();
        await using MemoryStream stream = Utf8("hello world");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream,
            "text/plain",
            maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("hello world");
        result.IsTruncated.ShouldBeFalse();
        result.CharCount.ShouldBe(11);
        result.ExtractorName.ShouldBe(PlainTextExtractor.ExtractorName);
        result.DetectedLanguage.ShouldBeNull();
    }

    [Fact]
    public async Task Honours_utf8_bom()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] payload = Encoding.UTF8.GetBytes("café");
        byte[] full = [.. bom, .. payload];
        await using MemoryStream stream = new(full);

        PlainTextExtractor extractor = CreateExtractor();
        TextExtractionResult result = await extractor.ExtractAsync(
            stream,
            "text/plain",
            maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("café");
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Truncates_and_flags_when_text_exceeds_cap()
    {
        PlainTextExtractor extractor = CreateExtractor();
        string payload = new('x', 1024);
        await using MemoryStream stream = Utf8(payload);

        TextExtractionResult result = await extractor.ExtractAsync(
            stream,
            "text/plain",
            maxCharLength: 100,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.Length.ShouldBe(100);
        result.CharCount.ShouldBe(100);
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions options = new() { MaxBodySizeBytes = 8 };
        PlainTextExtractor extractor = CreateExtractor(options);
        await using MemoryStream stream = Utf8(new string('a', 1024));

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await extractor.ExtractAsync(
                stream,
                "text/plain",
                maxCharLength: 1024,
                cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("text/html", true)]
    [InlineData("text/markdown", true)]
    [InlineData("application/pdf", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_text_family(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);
}
