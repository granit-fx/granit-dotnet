using Granit.DataExchange.Csv.Internal.Export;
using Granit.DataExchange.Csv.Internal.Import;
using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Csv.Tests;

public sealed class SepCsvFileParserTests
{
    private static readonly SepCsvFileParser Sut = new();
    private static readonly FileParsingOptions DefaultOptions = new();

    private static FileStream OpenTestFile(string fileName) =>
        new(Path.Join("TestData", fileName), FileMode.Open, FileAccess.Read);

    // ---- CanParse --------------------------------------------------------

    [Theory]
    [InlineData("text/csv")]
    [InlineData("TEXT/CSV")]
    [InlineData("application/csv")]
    public void CanParse_supported_mime_types_returns_true(string mimeType) =>
        Sut.CanParse(mimeType).ShouldBeTrue();

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public void CanParse_unsupported_mime_types_returns_false(string mimeType) =>
        Sut.CanParse(mimeType).ShouldBeFalse();

    // ---- ExtractHeadersAsync ---------------------------------------------

    [Fact]
    public async Task ExtractHeadersAsync_returns_column_names()
    {
        // Arrange
        using FileStream stream = OpenTestFile("simple.csv");

        // Act
        IReadOnlyList<string> headers = await Sut.ExtractHeadersAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken);

        // Assert
        headers.ShouldBe(["Name", "Email", "Age"]);
    }

    [Fact]
    public async Task ExtractHeadersAsync_with_semicolon_separator()
    {
        // Arrange
        using FileStream stream = OpenTestFile("semicolon.csv");
        FileParsingOptions options = new() { Separator = ";" };

        // Act
        IReadOnlyList<string> headers = await Sut.ExtractHeadersAsync(
            stream, options, TestContext.Current.CancellationToken);

        // Assert
        headers.ShouldBe(["Nom", "Prenom", "Ville"]);
    }

    // ---- ReadPreviewAsync ------------------------------------------------

    [Fact]
    public async Task ReadPreviewAsync_returns_limited_rows()
    {
        // Arrange
        using FileStream stream = OpenTestFile("simple.csv");

        // Act
        IReadOnlyList<string[]> rows = await Sut.ReadPreviewAsync(
            stream, DefaultOptions, maxRows: 2, TestContext.Current.CancellationToken);

        // Assert
        rows.Count.ShouldBe(2);
        rows[0].ShouldBe(["Alice", "alice@example.com", "30"]);
        rows[1].ShouldBe(["Bob", "bob@example.com", "25"]);
    }

    [Fact]
    public async Task ReadPreviewAsync_returns_all_when_fewer_than_max()
    {
        // Arrange
        using FileStream stream = OpenTestFile("semicolon.csv");
        FileParsingOptions options = new() { Separator = ";" };

        // Act
        IReadOnlyList<string[]> rows = await Sut.ReadPreviewAsync(
            stream, options, maxRows: 10, TestContext.Current.CancellationToken);

        // Assert
        rows.Count.ShouldBe(2);
    }

    // ---- ParseAsync ------------------------------------------------------

    [Fact]
    public async Task ParseAsync_streams_all_rows()
    {
        // Arrange
        using FileStream stream = OpenTestFile("simple.csv");
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken))
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
        using FileStream stream = OpenTestFile("simple.csv");
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows[0].RowNumber.ShouldBe(1);
        rows[1].RowNumber.ShouldBe(2);
        rows[4].RowNumber.ShouldBe(5);
    }

    [Fact]
    public async Task ParseAsync_handles_quoted_values()
    {
        // Arrange
        using FileStream stream = OpenTestFile("quoted.csv");
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows.Count.ShouldBe(2);
        rows[0].Values["Name"].ShouldBe("Smith, John");
        rows[0].Values["Address"].ShouldBe("123 Main St, Apt 4");
    }

    [Fact]
    public async Task ParseAsync_empty_values_are_null()
    {
        // Arrange
        using FileStream stream = OpenTestFile("empty-values.csv");
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows[0].Values["Phone"].ShouldBeNull();
        rows[1].Values["Email"].ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_utf8_bom_is_handled()
    {
        // Arrange
        using FileStream stream = OpenTestFile("utf8-bom.csv");
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, DefaultOptions, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows.Count.ShouldBe(2);
        rows[0].Values.ShouldContainKey("Name");
        rows[0].Values["Name"].ShouldBe("Alice");
        rows[0].Values["City"].ShouldBe("Brussels");
    }

    [Fact]
    public async Task ParseAsync_with_semicolon_separator()
    {
        // Arrange
        using FileStream stream = OpenTestFile("semicolon.csv");
        FileParsingOptions options = new() { Separator = ";" };
        List<RawImportRow> rows = [];

        // Act
        await foreach (RawImportRow row in Sut.ParseAsync(
            stream, options, TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        // Assert
        rows.Count.ShouldBe(2);
        rows[0].Values["Nom"].ShouldBe("Dupont");
        rows[0].Values["Prenom"].ShouldBe("Jean");
        rows[0].Values["Ville"].ShouldBe("Bruxelles");
    }
}
