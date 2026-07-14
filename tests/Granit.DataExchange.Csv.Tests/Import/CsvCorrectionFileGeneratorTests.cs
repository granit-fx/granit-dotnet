using System.Text;
using Granit.DataExchange.Csv.Internal.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Csv.Tests.Import;

public sealed class CsvCorrectionFileGeneratorTests
{
    private static readonly CsvCorrectionFileGenerator Sut = new();
    private static readonly FileParsingOptions DefaultOptions = new() { MimeType = "text/csv" };

    private const string FourRowCsv =
        "Name,Email,Age\r\n" +
        "Alice,alice@example.com,30\r\n" +
        "Bob,bad-email,25\r\n" +
        "Charlie,charlie@example.com,35\r\n" +
        "Diana,,28\r\n";

    // ---- Row filtering -----------------------------------------------

    [Fact]
    public async Task GenerateAsync_emits_only_failed_rows()
    {
        // Arrange
        ImportReport report = BuildReport(
        [
            new ImportRowError(2, ImportRowErrorKind.Conversion, ["Validation:Format:Email"], "Invalid email format."),
            new ImportRowError(4, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Email is required."),
        ]);

        // Act
        string csv = await GenerateCsvAsync(FourRowCsv, report);
        string[] lines = SplitLines(csv);

        // Assert
        lines[0].ShouldBe("Name,Email,Age,_ImportError");
        lines.Length.ShouldBe(3); // header + 2 failed rows
        lines[1].ShouldStartWith("Bob,bad-email,25,");
        lines[1].ShouldContain("Conversion: Validation:Format:Email");
        lines[1].ShouldContain("Invalid email format.");
        lines[2].ShouldStartWith("Diana,,28,");
        lines[2].ShouldContain("Validation: Validation:Builtin:NotEmpty");
        lines[2].ShouldContain("Email is required.");
    }

    [Fact]
    public async Task GenerateAsync_preserves_original_cell_values()
    {
        // Arrange
        ImportReport report = BuildReport(
        [
            new ImportRowError(1, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Name is required."),
        ]);

        // Act
        string csv = await GenerateCsvAsync(FourRowCsv, report);
        string[] lines = SplitLines(csv);

        // Assert
        lines.Length.ShouldBe(2);
        lines[1].ShouldStartWith("Alice,alice@example.com,30,");
    }

    // ---- Multiple errors on the same row ------------------------------

    [Fact]
    public async Task GenerateAsync_joins_multiple_errors_on_same_row_into_single_output_row()
    {
        // Arrange
        ImportReport report = BuildReport(
        [
            new ImportRowError(2, ImportRowErrorKind.Conversion, ["Validation:Format:Email"], "Invalid email format."),
            new ImportRowError(2, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Age is out of range."),
        ]);

        // Act
        string csv = await GenerateCsvAsync(FourRowCsv, report);
        string[] lines = SplitLines(csv);

        // Assert
        lines.Length.ShouldBe(2); // header + a single row for row 2
        lines[1].ShouldContain("Conversion: Validation:Format:Email");
        lines[1].ShouldContain("Invalid email format.");
        lines[1].ShouldContain(" | ");
        lines[1].ShouldContain("Validation: Validation:Builtin:NotEmpty");
        lines[1].ShouldContain("Age is out of range.");
    }

    // ---- Empty RowErrors ------------------------------------------------

    [Fact]
    public async Task GenerateAsync_with_empty_row_errors_emits_header_only()
    {
        // Arrange
        ImportReport report = BuildReport([]);

        // Act
        string csv = await GenerateCsvAsync(FourRowCsv, report);
        string[] lines = SplitLines(csv);

        // Assert
        lines.Length.ShouldBe(1);
        lines[0].ShouldBe("Name,Email,Age,_ImportError");
    }

    // ---- Formula-injection guard ---------------------------------------

    [Fact]
    public async Task GenerateAsync_quotes_error_column_containing_separator_and_embedded_quotes()
    {
        // Arrange: ImportRowError.Kind is always prefixed onto the formatted error text
        // (e.g. "Validation: ..."), so the error column can never itself start with a
        // formula-trigger char. The same WriteField guard is still exercised here through
        // its quoting/escaping rule (separator + embedded quotes), proving the error column
        // goes through the identical guarded path as CsvExportWriter's WriteField.
        const string csv =
            "Name,Email\r\n" +
            "Alice,alice@example.com\r\n";

        ImportReport report = BuildReport(
        [
            new ImportRowError(1, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Email \"format\" is invalid, please fix"),
        ]);

        // Act
        string result = await GenerateCsvAsync(csv, report);

        // Assert
        result.ShouldContain("\"Validation: Validation:Builtin:NotEmpty — Email \"\"format\"\" is invalid, please fix\"");
    }

    [Fact]
    public async Task GenerateAsync_applies_formula_injection_guard_on_reemitted_original_cells()
    {
        // Arrange
        const string csvWithFormulaLikeCell =
            "Name,Formula\r\n" +
            "Alice,=SUM(A1:A2)\r\n";

        ImportReport report = BuildReport(
        [
            new ImportRowError(1, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Some error."),
        ]);

        // Act
        string csv = await GenerateCsvAsync(csvWithFormulaLikeCell, report);

        // Assert
        csv.ShouldContain("\"'=SUM(A1:A2)\"");
    }

    // ---- Stream contract -------------------------------------------------

    [Fact]
    public async Task GenerateAsync_returns_stream_positioned_at_zero_and_readable()
    {
        // Arrange
        ImportReport report = BuildReport(
        [
            new ImportRowError(1, ImportRowErrorKind.Validation, ["Validation:Builtin:NotEmpty"], "Some error."),
        ]);

        await using MemoryStream input = new(Encoding.UTF8.GetBytes(FourRowCsv));

        // Act
        await using Stream result = await Sut.GenerateAsync(
            input, "text/csv", report, DefaultOptions, TestContext.Current.CancellationToken);

        // Assert
        result.CanRead.ShouldBeTrue();
        result.Position.ShouldBe(0);
    }

    // ---- Separator handling -----------------------------------------------

    [Fact]
    public async Task GenerateAsync_respects_semicolon_separator()
    {
        // Arrange
        const string semicolonCsv =
            "Nom;Prenom\r\n" +
            "Dupont;Jean\r\n" +
            "Martin;Paul\r\n";

        FileParsingOptions options = new() { Separator = ";", MimeType = "text/csv" };
        ImportReport report = BuildReport(
        [
            new ImportRowError(2, ImportRowErrorKind.Persistence, ["Validation:Builtin:Duplicate"], "Duplicate row."),
        ]);

        // Act
        await using MemoryStream input = new(Encoding.UTF8.GetBytes(semicolonCsv));
        await using Stream resultStream = await Sut.GenerateAsync(
            input, "text/csv", report, options, TestContext.Current.CancellationToken);
        using StreamReader reader = new(resultStream);
        string csv = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        string[] lines = SplitLines(csv);

        // Assert
        lines[0].ShouldBe("Nom;Prenom;_ImportError");
        lines.Length.ShouldBe(2);
        lines[1].ShouldStartWith("Martin;Paul;");
    }

    // ---- Helpers -----------------------------------------------------

    private static ImportReport BuildReport(IReadOnlyList<ImportRowError> rowErrors) => new()
    {
        TotalRows = 4,
        SucceededRows = 4 - rowErrors.Select(e => e.RowNumber).Distinct().Count(),
        FailedRows = rowErrors.Select(e => e.RowNumber).Distinct().Count(),
        SkippedRows = 0,
        InsertedRows = 0,
        UpdatedRows = 0,
        Duration = TimeSpan.FromSeconds(1),
        FinalStatus = ImportJobStatus.PartiallyCompleted,
        RowErrors = rowErrors,
    };

    private static async Task<string> GenerateCsvAsync(string sourceCsv, ImportReport report)
    {
        await using MemoryStream input = new(Encoding.UTF8.GetBytes(sourceCsv));
        await using Stream resultStream = await Sut.GenerateAsync(
            input, "text/csv", report, DefaultOptions, TestContext.Current.CancellationToken);
        using StreamReader reader = new(resultStream);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static string[] SplitLines(string csv) =>
        csv.TrimStart('\uFEFF').Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
