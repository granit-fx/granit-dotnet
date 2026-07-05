using ClosedXML.Excel;
using Granit.DataExchange.Excel.Internal.Export;
using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Excel.Tests;

public sealed class ClosedXmlExportWriterTests
{
    private static readonly ClosedXmlExportWriter Sut = new();

    // ---- CanWrite ----------------------------------------------------

    [Theory]
    [InlineData("xlsx")]
    [InlineData("XLSX")]
    [InlineData("Xlsx")]
    public void CanWrite_xlsx_returns_true(string format) =>
        Sut.CanWrite(format).ShouldBeTrue();

    [Theory]
    [InlineData("csv")]
    [InlineData("pdf")]
    [InlineData("")]
    public void CanWrite_other_formats_returns_false(string format) =>
        Sut.CanWrite(format).ShouldBeFalse();

    // ---- MimeType / FileExtension ------------------------------------

    [Fact]
    public void MimeType_is_xlsx() =>
        Sut.MimeType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    [Fact]
    public void FileExtension_is_xlsx() =>
        Sut.FileExtension.ShouldBe(".xlsx");

    // ---- WriteAsync --------------------------------------------------

    [Fact]
    public async Task WriteAsync_creates_workbook_with_headers_and_data()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Email", "String", null, null, 1, false),
            new("Age", "Int32", "Âge", null, 2, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com", ["Age"] = 30 },
            new Dictionary<string, object?> { ["Name"] = "Bob", ["Email"] = "bob@test.com", ["Age"] = 25 },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLWorksheet ws = workbook.Worksheets.First();

        ws.Cell(1, 1).GetString().ShouldBe("Nom");
        ws.Cell(1, 2).GetString().ShouldBe("Email");
        ws.Cell(1, 3).GetString().ShouldBe("Âge");
        ws.Cell(1, 1).Style.Font.Bold.ShouldBeTrue();

        ws.Cell(2, 1).GetString().ShouldBe("Alice");
        ws.Cell(2, 2).GetString().ShouldBe("alice@test.com");
        ws.Cell(2, 3).GetValue<int>().ShouldBe(30);

        ws.Cell(3, 1).GetString().ShouldBe("Bob");
        ws.Cell(3, 2).GetString().ShouldBe("bob@test.com");
        ws.Cell(3, 3).GetValue<int>().ShouldBe(25);
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
        await Sut.WriteAsync(stream, fields,
            ToAsyncEnumerable([]),
            TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLWorksheet ws = workbook.Worksheets.First();

        ws.Cell(1, 1).GetString().ShouldBe("Nom");
        ws.LastRowUsed()!.RowNumber().ShouldBe(1);
    }

    [Fact]
    public async Task WriteAsync_uses_property_path_when_no_header()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Company.Name", "String", null, null, 0, true),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Company.Name"] = "Acme" },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        workbook.Worksheets.First().Cell(1, 1).GetString().ShouldBe("Company.Name");
    }

    [Fact]
    public async Task WriteAsync_handles_null_values()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = null },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        workbook.Worksheets.First().Cell(2, 1).GetString().ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_formats_dates()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("BirthDate", "DateOnly", null, "yyyy-MM-dd", 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["BirthDate"] = new DateOnly(1990, 6, 15) },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.Style.DateFormat.Format.ShouldBe("yyyy-MM-dd");
    }

    // ---- SetCellValue: DateTime ----------------------------------------

    [Fact]
    public async Task WriteAsync_datetime_uses_default_date_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("CreatedAt", "DateTime", null, null, 0, false),
        ];

        var dt = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["CreatedAt"] = dt },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<DateTime>().ShouldBe(dt);
        cell.Style.DateFormat.Format.ShouldBe("dd/MM/yyyy");
    }

    [Fact]
    public async Task WriteAsync_datetime_uses_custom_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("CreatedAt", "DateTime", null, "yyyy-MM-dd HH:mm:ss", 0, false),
        ];

        var dt = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["CreatedAt"] = dt },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.Style.DateFormat.Format.ShouldBe("yyyy-MM-dd HH:mm:ss");
    }

    // ---- SetCellValue: DateTimeOffset -----------------------------------

    [Fact]
    public async Task WriteAsync_datetimeoffset_uses_default_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Timestamp", "DateTimeOffset", null, null, 0, false),
        ];

        var dto = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.FromHours(2));
        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Timestamp"] = dto },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<DateTime>().ShouldBe(dto.DateTime);
        cell.Style.DateFormat.Format.ShouldBe("dd/MM/yyyy HH:mm");
    }

    [Fact]
    public async Task WriteAsync_datetimeoffset_uses_custom_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Timestamp", "DateTimeOffset", null, "yyyy-MM-dd", 0, false),
        ];

        var dto = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.FromHours(2));
        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Timestamp"] = dto },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.Style.DateFormat.Format.ShouldBe("yyyy-MM-dd");
    }

    // ---- SetCellValue: DateOnly (default format) ------------------------

    [Fact]
    public async Task WriteAsync_dateonly_uses_default_format_when_no_custom()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("BirthDate", "DateOnly", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["BirthDate"] = new DateOnly(1990, 6, 15) },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<DateTime>().ShouldBe(new DateTime(1990, 6, 15));
        cell.Style.DateFormat.Format.ShouldBe("dd/MM/yyyy");
    }

    // ---- SetCellValue: decimal ------------------------------------------

    [Fact]
    public async Task WriteAsync_decimal_without_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Amount", "Decimal", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Amount"] = 123.45m },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<decimal>().ShouldBe(123.45m);
    }

    [Fact]
    public async Task WriteAsync_decimal_with_custom_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Amount", "Decimal", null, "#,##0.00", 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Amount"] = 1234.56m },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<decimal>().ShouldBe(1234.56m);
        cell.Style.NumberFormat.Format.ShouldBe("#,##0.00");
    }

    // ---- SetCellValue: double -------------------------------------------

    [Fact]
    public async Task WriteAsync_double_without_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Rate", "Double", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Rate"] = 3.14 },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<double>().ShouldBe(3.14);
    }

    [Fact]
    public async Task WriteAsync_double_with_custom_format()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Rate", "Double", null, "0.000", 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Rate"] = 3.14159 },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<double>().ShouldBe(3.14159);
        cell.Style.NumberFormat.Format.ShouldBe("0.000");
    }

    // ---- SetCellValue: long ---------------------------------------------

    [Fact]
    public async Task WriteAsync_long_value()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("BigId", "Int64", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["BigId"] = 9_876_543_210L },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<long>().ShouldBe(9_876_543_210L);
    }

    // ---- SetCellValue: bool ---------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_bool_value(bool value)
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Activated", "Boolean", null, null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Activated"] = value },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetValue<bool>().ShouldBe(value);
    }

    // ---- SetCellValue: default (ToString) -------------------------------

    [Fact]
    public async Task WriteAsync_unknown_type_uses_tostring()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Id", "Guid", null, null, 0, false),
        ];

        var guid = Guid.NewGuid();
        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Id"] = guid },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLCell cell = workbook.Worksheets.First().Cell(2, 1);
        cell.GetString().ShouldBe(guid.ToString());
    }

    // ---- WriteAsync: missing key ----------------------------------------

    [Fact]
    public async Task WriteAsync_missing_key_in_row_writes_null()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Missing", "String", null, null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice" },
        ];

        await using MemoryStream stream = new();

        // Act
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        // Assert
        stream.Position = 0;
        using XLWorkbook workbook = new(stream);
        IXLWorksheet ws = workbook.Worksheets.First();
        ws.Cell(2, 1).GetString().ShouldBe("Alice");
        ws.Cell(2, 2).GetString().ShouldBeEmpty();
    }

    // ---- Helpers -----------------------------------------------------

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsyncEnumerable(
        List<IReadOnlyDictionary<string, object?>> items)
    {
        foreach (IReadOnlyDictionary<string, object?> item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
