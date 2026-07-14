using System.Globalization;
using ClosedXML.Excel;
using Granit.DataExchange.Export;

namespace Granit.DataExchange.Excel.Internal.Export;

/// <summary>
/// <see cref="IExportWriter"/> implementation that produces <c>.xlsx</c> files using
/// <a href="https://closedxml.io/">ClosedXML</a>.
/// </summary>
/// <remarks>
/// <para>
/// Writes a single worksheet named <c>"Export"</c> with bold headers (row 1)
/// and data rows (row 2+). Columns are auto-sized to content.
/// </para>
/// <para>
/// Cell types are set based on the CLR value type: <c>DateTime</c>/<c>DateTimeOffset</c>
/// use date formatting, <c>decimal</c>/<c>double</c>/<c>int</c> use number formatting,
/// and everything else is written as text.
/// </para>
/// <para>
/// The xlsx format caps a worksheet at <see cref="MaxRowsPerSheet"/> rows. The writer checks the
/// cap per row and fails fast with an actionable message instead of buffering gigabytes before
/// ClosedXML rejects the workbook.
/// </para>
/// </remarks>
internal sealed class ClosedXmlExportWriter : IExportWriter
{
    /// <summary>
    /// Hard xlsx worksheet row limit (Office Open XML). One row is consumed by the header,
    /// so at most <see cref="MaxDataRows"/> data rows fit on the sheet.
    /// </summary>
    internal const int MaxRowsPerSheet = 1_048_576;

    /// <summary>
    /// Maximum number of data rows (<see cref="MaxRowsPerSheet"/> minus the header row).
    /// </summary>
    internal const int MaxDataRows = MaxRowsPerSheet - 1;

    /// <inheritdoc/>
    public bool CanWrite(string format) =>
        string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string MimeType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc/>
    public string FileExtension => ".xlsx";

    /// <inheritdoc/>
    public ExportFormatCapabilities Capabilities => ExportFormatCapabilities.TabularOnly;

    /// <inheritdoc/>
    public async Task<long> WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<object?[]> rows,
        CancellationToken cancellationToken = default)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet ws = workbook.AddWorksheet("Export");

        // Headers (row 1, bold)
        for (int col = 0; col < fields.Count; col++)
        {
            IXLCell cell = ws.Cell(1, col + 1);
            cell.SetValue(fields[col].Header ?? fields[col].PropertyPath);
            cell.Style.Font.Bold = true;
        }

        // Data rows (values aligned to the fields index)
        long rowCount = 0;
        int rowNumber = 2;
        await foreach (object?[] row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ThrowIfRowCapExceeded(rowCount);

            for (int col = 0; col < fields.Count; col++)
            {
                SetCellValue(ws.Cell(rowNumber, col + 1), row[col], fields[col]);
            }

            rowNumber++;
            rowCount++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(output);
        return rowCount;
    }

    /// <summary>
    /// Throws when writing one more data row would exceed <see cref="MaxDataRows"/>.
    /// Internal so the guard is unit-testable without generating a million rows.
    /// </summary>
    /// <param name="dataRowsWritten">The number of data rows already written to the sheet.</param>
    /// <exception cref="InvalidOperationException">The worksheet row cap is reached.</exception>
    internal static void ThrowIfRowCapExceeded(long dataRowsWritten)
    {
        if (dataRowsWritten >= MaxDataRows)
        {
            string rowLimit = MaxRowsPerSheet.ToString("N0", CultureInfo.InvariantCulture);
            string dataRowLimit = MaxDataRows.ToString("N0", CultureInfo.InvariantCulture);
            throw new InvalidOperationException(
                $"The xlsx format supports at most {rowLimit} rows per worksheet " +
                $"({dataRowLimit} data rows plus 1 header row) and the export exceeds that limit. " +
                "Use a streaming format such as 'csv' or 'json' for larger datasets, " +
                "or narrow the export with filters.");
        }
    }

    private static void SetCellValue(IXLCell cell, object? value, ExportFieldDescriptor field)
    {
        if (value is null)
        {
            return;
        }

        switch (value)
        {
            case DateTime dt:
                cell.SetValue(dt);
                cell.Style.DateFormat.Format = field.Format ?? "dd/MM/yyyy";
                break;

            case DateTimeOffset dto:
                cell.SetValue(dto.DateTime);
                cell.Style.DateFormat.Format = field.Format ?? "dd/MM/yyyy HH:mm";
                break;

            case DateOnly d:
                cell.SetValue(d.ToDateTime(TimeOnly.MinValue));
                cell.Style.DateFormat.Format = field.Format ?? "dd/MM/yyyy";
                break;

            case decimal m:
                cell.SetValue(m);
                if (field.Format is not null)
                {
                    cell.Style.NumberFormat.Format = field.Format;
                }

                break;

            case double d:
                cell.SetValue(d);
                if (field.Format is not null)
                {
                    cell.Style.NumberFormat.Format = field.Format;
                }

                break;

            case int i:
                cell.SetValue(i);
                break;

            case long l:
                cell.SetValue(l);
                break;

            case bool b:
                cell.SetValue(b);
                break;

            default:
                string text = value.ToString() ?? string.Empty;
                if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
                {
                    cell.SetValue($"'{text}");
                }
                else
                {
                    cell.SetValue(text);
                }

                break;
        }
    }
}
