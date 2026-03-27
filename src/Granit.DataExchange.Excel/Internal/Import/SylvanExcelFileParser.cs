using System.Runtime.CompilerServices;
using Granit.DataExchange.Import.Parsing;
using Sylvan.Data.Excel;

namespace Granit.DataExchange.Excel.Internal.Import;

/// <summary>
/// Excel file parser backed by <see href="https://github.com/MarkPflug/Sylvan.Data.Excel">Sylvan.Data.Excel</see>.
/// Supports <c>.xlsx</c>, <c>.xls</c> and <c>.xlsb</c> formats via streaming <see cref="ExcelDataReader"/>.
/// </summary>
internal sealed class SylvanExcelFileParser : IFileParser
{
    private static readonly string[] SupportedMimeTypes =
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
    ];

    /// <inheritdoc/>
    public bool CanParse(string mimeType) =>
        SupportedMimeTypes.Any(m => string.Equals(m, mimeType, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ExtractHeadersAsync(
        Stream stream,
        FileParsingOptions options,
        CancellationToken cancellationToken = default)
    {
        using ExcelDataReader reader = await CreateReaderAsync(stream, options, cancellationToken).ConfigureAwait(false);

        List<string> headers = new(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            headers.Add(reader.GetName(i));
        }

        return headers.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string[]>> ReadPreviewAsync(
        Stream stream,
        FileParsingOptions options,
        int maxRows = 10,
        CancellationToken cancellationToken = default)
    {
        using ExcelDataReader reader = await CreateReaderAsync(stream, options, cancellationToken).ConfigureAwait(false);
        int fieldCount = reader.FieldCount;

        List<string[]> rows = [];
        int count = 0;

        while (count < maxRows && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            int cellCount = Math.Min(fieldCount, reader.RowFieldCount);
            string[] values = new string[fieldCount];

            for (int i = 0; i < cellCount; i++)
            {
                values[i] = reader.GetString(i);
            }

            for (int i = cellCount; i < fieldCount; i++)
            {
                values[i] = string.Empty;
            }

            rows.Add(values);
            count++;
        }

        return rows.AsReadOnly();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<RawImportRow> ParseAsync(
        Stream stream,
        FileParsingOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using ExcelDataReader reader = await CreateReaderAsync(stream, options, cancellationToken).ConfigureAwait(false);

        List<string> headers = new(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            headers.Add(reader.GetName(i));
        }

        int rowNumber = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rowNumber++;
            int cellCount = Math.Min(headers.Count, reader.RowFieldCount);
            Dictionary<string, string?> values = new(headers.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < cellCount; i++)
            {
                string value = reader.GetString(i);
                values[headers[i]] = value.Length == 0 ? null : value;
            }

            for (int i = cellCount; i < headers.Count; i++)
            {
                values[headers[i]] = null;
            }

            yield return new RawImportRow(rowNumber, values);
        }
    }

    private static async Task<ExcelDataReader> CreateReaderAsync(
        Stream stream,
        FileParsingOptions options,
        CancellationToken cancellationToken)
    {
        ExcelWorkbookType workbookType = GetWorkbookType(options.MimeType);
        ExcelDataReaderOptions readerOptions = new() { OwnsStream = false };
        ExcelDataReader reader = await ExcelDataReader.CreateAsync(stream, workbookType, readerOptions, cancellationToken).ConfigureAwait(false);

        if (options.SheetName is not null)
        {
            reader.TryOpenWorksheet(options.SheetName);
        }

        return reader;
    }

    private static ExcelWorkbookType GetWorkbookType(string? mimeType) =>
        mimeType?.ToLowerInvariant() switch
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                => ExcelWorkbookType.ExcelXml,
            "application/vnd.ms-excel"
                => ExcelWorkbookType.Excel,
            _ => ExcelWorkbookType.ExcelXml,
        };
}
