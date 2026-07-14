using System.Text;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Reporting;
using nietras.SeparatedValues;

namespace Granit.DataExchange.Csv.Internal.Import;

/// <summary>
/// <see cref="ICorrectionFileGenerator"/> implementation that produces a CSV correction file
/// containing only the rows that failed import, annotated with an appended error column.
/// </summary>
/// <remarks>
/// Re-reads the original file using the same Sep-based reader configuration as
/// <see cref="SepCsvFileParser"/> (separator, encoding, header handling) so the emitted rows
/// are a faithful round-trip of the original columns. Row-level errors are grouped by
/// <see cref="ImportRowError.RowNumber"/> so a row with multiple errors is emitted once.
/// </remarks>
internal sealed class CsvCorrectionFileGenerator : ICorrectionFileGenerator
{
    private const string ErrorColumnName = "_ImportError";
    private const char Quote = '"';
    private static readonly char[] FormulaTriggerChars = ['=', '+', '-', '@', '\t', '\r'];

    /// <inheritdoc/>
    public async Task<Stream> GenerateAsync(
        Stream originalFileStream,
        string mimeType,
        ImportReport report,
        FileParsingOptions parsingOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalFileStream);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(parsingOptions);

        Dictionary<int, string> errorsByRow = BuildErrorsByRow(report.RowErrors);
        char separator = parsingOptions.Separator.Length > 0 ? parsingOptions.Separator[0] : ',';

        MemoryStream output = new();
        using (SepReader reader = CreateReader(originalFileStream, parsingOptions, separator))
        {
            await using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

            IReadOnlyList<string> colNames = reader.Header.ColNames;
            WriteHeader(writer, colNames, separator);

            int rowNumber = 0;
            foreach (SepReader.Row row in reader)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                if (!errorsByRow.TryGetValue(rowNumber, out string? errorText))
                {
                    continue;
                }

                WriteDataRow(writer, row, colNames, errorText, separator);
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        output.Position = 0;
        return output;
    }

    private static Dictionary<int, string> BuildErrorsByRow(IReadOnlyList<ImportRowError> rowErrors) =>
        rowErrors
            .GroupBy(e => e.RowNumber)
            .ToDictionary(g => g.Key, g => string.Join(" | ", g.Select(FormatError)));

    private static string FormatError(ImportRowError error) =>
        $"{error.Kind}: {string.Join(", ", error.ErrorCodes)} — {error.Message}";

    private static SepReader CreateReader(Stream stream, FileParsingOptions options, char separator)
    {
        SepReaderOptions readerOptions = Sep.New(separator).Reader(o => o with { HasHeader = true, Unescape = true });

        if (options.Encoding is not null)
        {
            var encoding = Encoding.GetEncoding(options.Encoding);
            StreamReader textReader = new(stream, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return readerOptions.From(textReader);
        }

        return readerOptions.From(stream);
    }

    private static void WriteHeader(StreamWriter writer, IReadOnlyList<string> colNames, char separator)
    {
        for (int i = 0; i < colNames.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(separator);
            }

            WriteField(writer, colNames[i], separator);
        }

        writer.Write(separator);
        WriteField(writer, ErrorColumnName, separator);
        writer.WriteLine();
    }

    private static void WriteDataRow(StreamWriter writer, SepReader.Row row, IReadOnlyList<string> colNames, string errorText, char separator)
    {
        for (int i = 0; i < colNames.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(separator);
            }

            WriteField(writer, row[i].ToString(), separator);
        }

        writer.Write(separator);
        WriteField(writer, errorText, separator);
        writer.WriteLine();
    }

    private static void WriteField(StreamWriter writer, string value, char separator)
    {
        bool needsFormulaProtection = value.Length > 0 && FormulaTriggerChars.Contains(value[0]);

        if (needsFormulaProtection || value.Contains(separator) || value.Contains(Quote) || value.Contains('\n') || value.Contains('\r'))
        {
            writer.Write(Quote);
            if (needsFormulaProtection)
            {
                writer.Write('\'');
            }

            writer.Write(value.Replace("\"", "\"\""));
            writer.Write(Quote);
        }
        else
        {
            writer.Write(value);
        }
    }
}
