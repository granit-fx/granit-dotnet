using Granit.DataExchange.Csv.Internal.Export;
using Granit.DataExchange.Csv.Internal.Import;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Csv.Tests;

/// <summary>
/// Roundtrip tests: export to CSV then reimport via Sep parser.
/// Verifies that data survives the export→import cycle without loss.
/// </summary>
public sealed class CsvRoundtripTests
{
    private static readonly CsvExportWriter Writer = new();
    private static readonly SepCsvFileParser Parser = new();

    private static readonly FileParsingOptions ImportOptions = new()
    {
        Separator = ";",
    };

    // ── Headers ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_headers_match()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Email", "String", null, null, 1, false),
            new("Company.Name", "String", "Société", null, 2, true),
        ];

        await using MemoryStream stream = new();

        // Act — export
        await Writer.WriteAsync(stream, fields,
            ToAsyncEnumerable([]),
            TestContext.Current.CancellationToken);

        stream.Position = 0;

        // Act — reimport headers
        IReadOnlyList<string> headers = await Parser.ExtractHeadersAsync(
            stream, ImportOptions, TestContext.Current.CancellationToken);

        // Assert
        headers.Count.ShouldBe(3);
        headers[0].ShouldBe("Nom");
        headers[1].ShouldBe("Email");
        headers[2].ShouldBe("Société");
    }

    // ── String values ────────────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_string_values_preserved()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Email", "String", "Email", null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com" },
            new Dictionary<string, object?> { ["Name"] = "Bob", ["Email"] = "bob@test.com" },
        ];

        // Act
        List<RawImportRow> imported = await ExportThenImport(fields, rows);

        // Assert
        imported.Count.ShouldBe(2);
        imported[0].Values["Nom"].ShouldBe("Alice");
        imported[0].Values["Email"].ShouldBe("alice@test.com");
        imported[1].Values["Nom"].ShouldBe("Bob");
        imported[1].Values["Email"].ShouldBe("bob@test.com");
    }

    // ── Null symmetry ────────────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_null_values_symmetry()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Email", "String", "Email", null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = null },
            new Dictionary<string, object?> { ["Name"] = null, ["Email"] = "bob@test.com" },
        ];

        // Act
        List<RawImportRow> imported = await ExportThenImport(fields, rows);

        // Assert — null → empty string (export) → null (import)
        imported[0].Values["Email"].ShouldBeNull();
        imported[1].Values["Nom"].ShouldBeNull();
        imported[0].Values["Nom"].ShouldBe("Alice");
        imported[1].Values["Email"].ShouldBe("bob@test.com");
    }

    // ── Date format ──────────────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_date_format_preserved_as_string()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("BirthDate", "DateOnly", "Date", "dd/MM/yyyy", 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["BirthDate"] = new DateOnly(1990, 6, 15) },
        ];

        // Act
        List<RawImportRow> imported = await ExportThenImport(fields, rows);

        // Assert — date is received as formatted string
        imported[0].Values["Date"].ShouldBe("15/06/1990");
    }

    // ── Quoted values with separator ─────────────────────────────────────

    [Fact]
    public async Task Roundtrip_quoted_values_with_separator_preserved()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", "Nom", null, 0, false),
            new("Address", "String", "Adresse", null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Smith; John", ["Address"] = "123 \"Main\" St" },
        ];

        // Act
        List<RawImportRow> imported = await ExportThenImport(fields, rows);

        // Assert — quoting preserves special characters
        imported[0].Values["Nom"].ShouldBe("Smith; John");
        imported[0].Values["Adresse"].ShouldBe("123 \"Main\" St");
    }

    // ── Row count ────────────────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_row_count_matches()
    {
        // Arrange
        List<ExportFieldDescriptor> fields =
        [
            new("Id", "String", "Id", null, 0, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows = [];
        for (int i = 1; i <= 50; i++)
        {
            rows.Add(new Dictionary<string, object?> { ["Id"] = i.ToString() });
        }

        // Act
        List<RawImportRow> imported = await ExportThenImport(fields, rows);

        // Assert
        imported.Count.ShouldBe(50);
        imported[0].Values["Id"].ShouldBe("1");
        imported[49].Values["Id"].ShouldBe("50");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task<List<RawImportRow>> ExportThenImport(
        List<ExportFieldDescriptor> fields,
        List<IReadOnlyDictionary<string, object?>> rows)
    {
        await using MemoryStream stream = new();

        // Export
        await Writer.WriteAsync(stream, fields, ToAsyncEnumerable(rows),
            TestContext.Current.CancellationToken);

        // Reimport
        stream.Position = 0;
        List<RawImportRow> imported = [];
        await foreach (RawImportRow row in Parser.ParseAsync(stream, ImportOptions,
            TestContext.Current.CancellationToken))
        {
            imported.Add(row);
        }

        return imported;
    }

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
