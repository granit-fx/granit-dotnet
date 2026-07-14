using System.Text;
using Granit.DataExchange.Csv.Internal.Export;
using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Csv.Tests;

public sealed class CsvExportWriterTests
{
    private static readonly CsvExportWriter Sut = new();

    // ---- CanWrite ----------------------------------------------------

    [Theory]
    [InlineData("csv")]
    [InlineData("CSV")]
    [InlineData("Csv")]
    public void CanWrite_csv_returns_true(string format) =>
        Sut.CanWrite(format).ShouldBeTrue();

    [Theory]
    [InlineData("xlsx")]
    [InlineData("pdf")]
    [InlineData("")]
    public void CanWrite_other_formats_returns_false(string format) =>
        Sut.CanWrite(format).ShouldBeFalse();

    // ---- MimeType / FileExtension ------------------------------------

    [Fact]
    public void MimeType_is_text_csv() =>
        Sut.MimeType.ShouldBe("text/csv");

    [Fact]
    public void FileExtension_is_csv() =>
        Sut.FileExtension.ShouldBe(".csv");

    // ---- WriteAsync --------------------------------------------------

    [Fact]
    public async Task WriteAsync_produces_semicolon_separated_output()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Email", "String", null, null, 1, false),
        ];

        List<object?[]> rows =
        [
            ["Alice", "alice@test.com"],
            ["Bob", "bob@test.com"],
        ];

        await using MemoryStream stream = new();

        // Act
        long rowCount = await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        rowCount.ShouldBe(2);
        string csv = ReadCsv(stream);
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines[0].ShouldBe("Nom;Email");
        lines[1].ShouldBe("Alice;alice@test.com");
        lines[2].ShouldBe("Bob;bob@test.com");
    }

    [Fact]
    public async Task WriteAsync_empty_data_produces_headers_only()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
        ];

        await using MemoryStream stream = new();

        // Act
        long rowCount = await Sut.WriteAsync(stream, fields,
            ToAsyncEnumerable([]),
            TestContext.Current.CancellationToken);

        // Assert
        rowCount.ShouldBe(0);
        string csv = ReadCsv(stream);
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(1);
        lines[0].ShouldBe("Nom");
    }

    [Fact]
    public async Task WriteAsync_quotes_fields_with_separator()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
        ];

        List<object?[]> rows =
        [
            ["Smith; John"],
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        string csv = ReadCsv(stream);
        csv.ShouldContain("\"Smith; John\"");
    }

    [Fact]
    public async Task WriteAsync_quotes_fields_with_double_quotes()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
        ];

        List<object?[]> rows =
        [
            ["John \"Jack\" Doe"],
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        string csv = ReadCsv(stream);
        csv.ShouldContain("\"John \"\"Jack\"\" Doe\"");
    }

    [Fact]
    public async Task WriteAsync_null_values_produce_empty_fields()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Email", "String", null, null, 1, false),
        ];

        List<object?[]> rows =
        [
            ["Alice", null],
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        string csv = ReadCsv(stream);
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[1].ShouldBe("Alice;");
    }

    [Fact]
    public async Task WriteAsync_formats_dates()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Date", "DateOnly", null, "yyyy-MM-dd", 0, false),
        ];

        List<object?[]> rows =
        [
            [new DateOnly(2026, 3, 3)],
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        string csv = ReadCsv(stream);
        csv.ShouldContain("2026-03-03");
    }

    [Fact]
    public async Task WriteAsync_writes_utf8_bom()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields,
            ToAsyncEnumerable([]),
            TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        byte[] bom = new byte[3];
        _ = await stream.ReadAsync(bom, TestContext.Current.CancellationToken);
        bom.ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    public async Task WriteAsync_uses_property_path_when_no_header()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Company.Name", "String", null, null, 0, true),
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields,
            ToAsyncEnumerable([]),
            TestContext.Current.CancellationToken);

        // Assert
        string csv = ReadCsv(stream);
        csv.ShouldStartWith("Company.Name");
    }

    // ---- Helpers -----------------------------------------------------

    private static string ReadCsv(MemoryStream stream)
    {
        stream.Position = 0;
        return new UTF8Encoding(false).GetString(stream.ToArray()).TrimStart('\uFEFF');
    }

    private static async IAsyncEnumerable<object?[]> ToAsyncEnumerable(List<object?[]> items)
    {
        foreach (object?[] item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
