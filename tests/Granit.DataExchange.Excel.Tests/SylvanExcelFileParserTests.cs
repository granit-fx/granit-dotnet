using Granit.DataExchange.Excel.Internal.Import;
using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Excel.Tests;

public sealed class SylvanExcelFileParserTests
{
    private static readonly SylvanExcelFileParser Sut = new();

    private static readonly FileParsingOptions XlsxOptions = new()
    {
        MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    // ---- CanParse --------------------------------------------------------

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/vnd.ms-excel")]
    public void CanParse_supported_mime_types_returns_true(string mimeType) =>
        Sut.CanParse(mimeType).ShouldBeTrue();

    [Fact]
    public void CanParse_is_case_insensitive() =>
        Sut.CanParse("APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.SPREADSHEETML.SHEET").ShouldBeTrue();

    [Theory]
    [InlineData("text/csv")]
    [InlineData("application/json")]
    [InlineData("application/pdf")]
    public void CanParse_unsupported_mime_types_returns_false(string mimeType) =>
        Sut.CanParse(mimeType).ShouldBeFalse();

    // ---- ExtractHeadersAsync ---------------------------------------------

    [Fact]
    public async Task ExtractHeadersAsync_returns_column_names()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateSimpleXlsx();

        // Act
        IReadOnlyList<string> headers = await Sut.ExtractHeadersAsync(
            stream, XlsxOptions, TestContext.Current.CancellationToken);

        // Assert
        headers.ShouldBe(["Name", "Email", "Age"]);
    }

    // ---- ReadPreviewAsync ------------------------------------------------

    [Fact]
    public async Task ReadPreviewAsync_returns_limited_rows()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateSimpleXlsx();

        // Act
        IReadOnlyList<string[]> rows = await Sut.ReadPreviewAsync(
            stream, XlsxOptions, maxRows: 2, TestContext.Current.CancellationToken);

        // Assert
        rows.Count.ShouldBe(2);
        rows[0].ShouldBe(["Alice", "alice@example.com", "30"]);
        rows[1].ShouldBe(["Bob", "bob@example.com", "25"]);
    }

    [Fact]
    public async Task ReadPreviewAsync_returns_all_when_fewer_than_max()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateSimpleXlsx();

        // Act
        IReadOnlyList<string[]> rows = await Sut.ReadPreviewAsync(
            stream, XlsxOptions, maxRows: 100, TestContext.Current.CancellationToken);

        // Assert
        rows.Count.ShouldBe(5);
    }

    // ---- ParseAsync ------------------------------------------------------

    [Fact]
    public async Task ParseAsync_streams_all_rows()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateSimpleXlsx();
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, XlsxOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows.Count.ShouldBe(5);
        rows[0].Values["Name"].ShouldBe("Alice");
        rows[0].Values["Email"].ShouldBe("alice@example.com");
        rows[0].Values["Age"].ShouldBe("30");
        rows[4].Values["Name"].ShouldBe("Eve");
    }

    [Fact]
    public async Task ParseAsync_row_numbers_are_one_based()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateSimpleXlsx();
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, XlsxOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows[0].RowNumber.ShouldBe(1);
        rows[1].RowNumber.ShouldBe(2);
        rows[4].RowNumber.ShouldBe(5);
    }

    [Fact]
    public async Task ParseAsync_empty_cells_are_null()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateXlsxWithEmptyCells();
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, XlsxOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows[0].Values["Phone"].ShouldBeNull();
        rows[1].Values["Email"].ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_with_sheet_name_selects_correct_sheet()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateMultiSheetXlsx();
        FileParsingOptions options = new() { MimeType = XlsxOptions.MimeType, SheetName = "Lookup" };
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, options, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows.Count.ShouldBe(3);
        rows[0].Values["Code"].ShouldBe("A");
        rows[0].Values["Label"].ShouldBe("Alpha");
        rows[2].Values["Code"].ShouldBe("C");
        rows[2].Values["Label"].ShouldBe("Gamma");
    }

    [Fact]
    public async Task ExtractHeadersAsync_with_sheet_name()
    {
        // Arrange
        await using MemoryStream stream = TestExcelHelper.CreateMultiSheetXlsx();
        FileParsingOptions options = new() { MimeType = XlsxOptions.MimeType, SheetName = "Lookup" };

        // Act
        IReadOnlyList<string> headers = await Sut.ExtractHeadersAsync(
            stream, options, TestContext.Current.CancellationToken);

        // Assert
        headers.ShouldBe(["Code", "Label"]);
    }
}
