using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Granit.TextExtraction.Office.Internal;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Office;

/// <summary>
/// Extractor for <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c>
/// (.xlsx). Iterates each <see cref="WorksheetPart"/>, resolves shared-string cells through
/// the workbook's <see cref="SharedStringTablePart"/>, and joins cells with tabs and rows
/// with newlines.
/// </summary>
public sealed partial class ExcelTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.excel";

    private const string Xlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly GranitTextExtractionOptions _options;
    private readonly ILogger<ExcelTextExtractor> _logger;

    public ExcelTextExtractor(IOptions<GranitTextExtractionOptions> options, ILogger<ExcelTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.Equals(Xlsx, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        byte[] bytes = await OpenXmlExtraction
            .ReadAllBytesAsync(source, _options.MaxBodySizeBytes, cancellationToken)
            .ConfigureAwait(false);

        OpenXmlGate.GateResult gate = OpenXmlGate.Inspect(bytes, _options);
        if (gate != OpenXmlGate.GateResult.Ok)
        {
            LogPackageRejected(gate.ToString());
            return OpenXmlExtraction.Skipped(ExtractorName);
        }

        try
        {
            using MemoryStream pkg = new(bytes, writable: false);
            using var doc = SpreadsheetDocument.Open(
                pkg, isEditable: false, OpenXmlExtraction.BuildOpenSettings(_options));

            WorkbookPart? workbookPart = doc.WorkbookPart;
            if (workbookPart is null)
            {
                return OpenXmlExtraction.Truncate(string.Empty, maxCharLength, ExtractorName);
            }

            SharedStringTable? sharedStrings = workbookPart
                .SharedStringTablePart?.SharedStringTable;

            StringBuilder sb = new(Math.Min(maxCharLength, 8192));
            bool truncated = false;

            foreach (WorksheetPart worksheetPart in workbookPart.WorksheetParts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sb.Length > 0)
                {
                    AppendIfRoom(sb, '\n', maxCharLength, ref truncated);
                    if (truncated) { break; }
                }

                Worksheet? worksheet = worksheetPart.Worksheet;
                if (worksheet is null) { continue; }

                foreach (Row row in worksheet.Descendants<Row>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool firstCellInRow = true;
                    foreach (Cell cell in row.Descendants<Cell>())
                    {
                        if (!firstCellInRow)
                        {
                            AppendIfRoom(sb, '\t', maxCharLength, ref truncated);
                            if (truncated) { break; }
                        }

                        string cellText = ResolveCellText(cell, sharedStrings);
                        AppendIfRoom(sb, cellText, maxCharLength, ref truncated);
                        if (truncated) { break; }

                        firstCellInRow = false;
                    }

                    if (truncated) { break; }
                    AppendIfRoom(sb, '\n', maxCharLength, ref truncated);
                    if (truncated) { break; }
                }

                if (truncated) { break; }
            }

            return new TextExtractionResult(
                Content: sb.ToString(),
                DetectedLanguage: null,
                IsTruncated: truncated,
                CharCount: sb.Length,
                ExtractorName: ExtractorName,
                Confidence: ExtractionConfidence.Deterministic);
        }
        catch (Exception ex) when (IsOpenXmlException(ex))
        {
            LogPackageParseFailed(ex);
            return OpenXmlExtraction.Skipped(ExtractorName);
        }
    }

    private static string ResolveCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null)
        {
            if (int.TryParse(cell.CellValue?.InnerText, out int index)
                && index >= 0
                && index < sharedStrings.ChildElements.Count)
            {
                return sharedStrings.ChildElements[index].InnerText;
            }
            return string.Empty;
        }

        // Inline strings, numbers, booleans, dates — InnerText is what's shown on the cell.
        return cell.CellValue?.InnerText
            ?? cell.InlineString?.InnerText
            ?? string.Empty;
    }

    private static void AppendIfRoom(StringBuilder sb, char c, int maxCharLength, ref bool truncated)
    {
        if (sb.Length >= maxCharLength)
        {
            truncated = true;
            return;
        }
        sb.Append(c);
    }

    private static void AppendIfRoom(StringBuilder sb, string s, int maxCharLength, ref bool truncated)
    {
        if (string.IsNullOrEmpty(s)) { return; }

        int remaining = maxCharLength - sb.Length;
        if (remaining <= 0)
        {
            truncated = true;
            return;
        }

        if (s.Length <= remaining)
        {
            sb.Append(s);
            return;
        }

        sb.Append(s, 0, remaining);
        truncated = true;
    }

    private static bool IsOpenXmlException(Exception ex) =>
        ex.GetType().Namespace?.StartsWith("DocumentFormat.OpenXml", StringComparison.Ordinal) == true
        || ex is FileFormatException
        || ex is InvalidDataException;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "ExcelTextExtractor rejected an .xlsx package: {Reason}.")]
    private partial void LogPackageRejected(string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "ExcelTextExtractor failed to parse an .xlsx package.")]
    private partial void LogPackageParseFailed(Exception exception);
}
