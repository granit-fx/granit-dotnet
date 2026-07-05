using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Office.Tests;

public sealed class ExcelTextExtractorTests
{
    private const string Xlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static ExcelTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(MEOptions.Create(options ?? new ExtractionOptions()),
            NullLogger<ExcelTextExtractor>.Instance);

    [Theory]
    [InlineData(Xlsx, true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_xlsx(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_cell_text_across_rows_and_columns()
    {
        string[,] sheet1 =
        {
            { "Header A", "Header B" },
            { "Alpha", "Beta" },
            { "Gamma", "Delta" },
        };
        byte[] xlsx = OfficeFixtures.Xlsx(("Sheet1", sheet1));
        await using MemoryStream stream = new(xlsx);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Xlsx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Header A");
        result.Content.ShouldContain("Beta");
        result.Content.ShouldContain("Delta");
        result.ExtractorName.ShouldBe(ExcelTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Handles_multiple_worksheets()
    {
        byte[] xlsx = OfficeFixtures.Xlsx(
            ("Sheet1", new[,] { { "First sheet content" } }),
            ("Sheet2", new[,] { { "Second sheet content" } }));
        await using MemoryStream stream = new(xlsx);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Xlsx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("First sheet content");
        result.Content.ShouldContain("Second sheet content");
    }

    [Fact]
    public async Task Truncates_to_max_char_length_and_flags()
    {
        // A single big cell — fixture generator only accepts string[,] so we wedge the
        // content into one position.
        string[,] big = { { new string('x', 5_000) } };
        byte[] xlsx = OfficeFixtures.Xlsx(("Sheet1", big));
        await using MemoryStream stream = new(xlsx);

        TextExtractionResult result = await CreateExtractor().ExtractAsync(
            stream, Xlsx, maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Too_many_zip_entries_returns_skipped_result()
    {
        ExtractionOptions options = new() { MaxZipEntries = 5 };
        byte[] adversarial = OfficeFixtures.ZipWithEntryCount(entries: 10);
        await using MemoryStream stream = new(adversarial);

        TextExtractionResult result = await CreateExtractor(options).ExtractAsync(
            stream, Xlsx, maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }
}
