using Granit.TextExtraction.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Pdf.Tests;

public sealed class PdfTextExtractorTests
{
    private static PdfTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(MEOptions.Create(options ?? new ExtractionOptions()),
            NullLogger<PdfTextExtractor>.Instance);

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("text/html", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_application_pdf(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_text_from_single_page()
    {
        byte[] pdf = PdfFixtures.TextOnly("Hello world", "Second line");
        using MemoryStream stream = new(pdf);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "application/pdf", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Hello world");
        result.Content.ShouldContain("Second line");
        result.ExtractorName.ShouldBe(PdfTextExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Joins_pages_with_blank_lines()
    {
        byte[] pdf = PdfFixtures.MultiPage(3);
        using MemoryStream stream = new(pdf);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "application/pdf", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Page 1");
        result.Content.ShouldContain("Page 2");
        result.Content.ShouldContain("Page 3");
        // Pages joined by "\n\n" — at least one blank-line separator must appear.
        result.Content.ShouldContain("\n\n");
    }

    [Fact]
    public async Task Stops_iterating_pages_once_cap_is_reached()
    {
        // Each page contains a long string — capping at a small char count should
        // truncate after the first or second page, not walk all 20.
        byte[] pdf = PdfFixtures.MultiPage(20, textPrefix: new string('x', 200));
        using MemoryStream stream = new(pdf);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "application/pdf", maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
        result.CharCount.ShouldBe(100);
    }

    [Fact]
    public async Task Empty_pdf_returns_empty_content()
    {
        byte[] pdf = PdfFixtures.Empty();
        using MemoryStream stream = new(pdf);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "application/pdf", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.Trim().ShouldBeEmpty();
        result.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Malformed_pdf_is_soft_skipped()
    {
        byte[] pdf = PdfFixtures.Malformed();
        using MemoryStream stream = new(pdf);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, "application/pdf", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        // Soft-skip contract: empty content + IsTruncated flag, no exception.
        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
        result.ExtractorName.ShouldBe(PdfTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        // Generate a real PDF that exceeds the configured cap (Empty PDF is ~700 bytes).
        ExtractionOptions options = new() { MaxBodySizeBytes = 32 };
        byte[] pdf = PdfFixtures.TextOnly("Some text");
        using MemoryStream stream = new(pdf);

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await CreateExtractor(options).ExtractAsync(
                stream, "application/pdf", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }
}
