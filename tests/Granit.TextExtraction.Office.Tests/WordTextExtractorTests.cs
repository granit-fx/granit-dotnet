using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Office.Tests;

public sealed class WordTextExtractorTests
{
    private const string Docx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static WordTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(MEOptions.Create(options ?? new ExtractionOptions()),
            NullLogger<WordTextExtractor>.Instance);

    [Theory]
    [InlineData(Docx, true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_docx(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_paragraph_text()
    {
        byte[] doc = OfficeFixtures.Docx("Hello world", "Second paragraph");
        using MemoryStream stream = new(doc);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Docx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Hello world");
        result.Content.ShouldContain("Second paragraph");
        result.ExtractorName.ShouldBe(WordTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Truncates_to_max_char_length_and_flags()
    {
        byte[] doc = OfficeFixtures.Docx(new string('x', 5_000));
        using MemoryStream stream = new(doc);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Docx, maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Too_many_zip_entries_returns_skipped_result()
    {
        ExtractionOptions options = new() { MaxZipEntries = 5 };
        byte[] adversarial = OfficeFixtures.ZipWithEntryCount(entries: 10);
        using MemoryStream stream = new(adversarial);

        TextExtractionResult result = await CreateExtractor(options).ExtractAsync(
            stream, Docx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Too_large_decompressed_returns_skipped_result()
    {
        ExtractionOptions options = new() { MaxDecompressedBytes = 1024 };
        byte[] adversarial = OfficeFixtures.ZipWithAdvertisedSize(advertisedBytes: 64 * 1024);
        using MemoryStream stream = new(adversarial);

        TextExtractionResult result = await CreateExtractor(options).ExtractAsync(
            stream, Docx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Malformed_zip_returns_skipped_result()
    {
        byte[] bytes = OfficeFixtures.InvalidZip();
        using MemoryStream stream = new(bytes);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Docx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }
}
