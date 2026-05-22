namespace Granit.DataExchange.Import.Reporting;

/// <summary>
/// Describes an error on a specific row during import.
/// </summary>
/// <param name="RowNumber">One-based row number in the source file.</param>
/// <param name="Kind">The category of error.</param>
/// <param name="ErrorCodes">Structured error codes (e.g. <c>["Validation:NotEmpty"]</c>).</param>
/// <param name="Message">Human-readable concatenated error message.</param>
public sealed record ImportRowError(
    int RowNumber,
    ImportRowErrorKind Kind,
    IReadOnlyList<string> ErrorCodes,
    string Message);
