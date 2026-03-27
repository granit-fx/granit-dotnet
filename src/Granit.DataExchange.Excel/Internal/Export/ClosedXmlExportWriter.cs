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
/// </remarks>
internal sealed class ClosedXmlExportWriter : IExportWriter
{
    /// <inheritdoc/>
    public bool CanWrite(string format) =>
        string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string MimeType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc/>
    public string FileExtension => ".xlsx";

    /// <inheritdoc/>
    public async Task WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
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

        // Data rows
        int row = 2;
        await foreach (IReadOnlyDictionary<string, object?> data in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            for (int col = 0; col < fields.Count; col++)
            {
                object? value = data.GetValueOrDefault(fields[col].PropertyPath);
                SetCellValue(ws.Cell(row, col + 1), value, fields[col]);
            }

            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(output);
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
