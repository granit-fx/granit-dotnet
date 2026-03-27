using System.Globalization;
using System.Text;
using Granit.DataExchange.Export;

namespace Granit.DataExchange.Csv.Internal.Export;

/// <summary>
/// <see cref="IExportWriter"/> implementation that produces RFC 4180-compliant <c>.csv</c> files.
/// </summary>
/// <remarks>
/// <para>
/// Uses UTF-8 with BOM for Excel compatibility. Fields containing the separator,
/// double quotes, or newlines are quoted. The separator is <c>;</c> (semicolon)
/// for European locale compatibility (Excel opens <c>;</c>-separated files correctly
/// in fr-FR, nl-BE, de-DE locales).
/// </para>
/// </remarks>
internal sealed class CsvExportWriter : IExportWriter
{
    private const char Separator = ';';
    private const char Quote = '"';

    /// <inheritdoc/>
    public bool CanWrite(string format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string MimeType => "text/csv";

    /// <inheritdoc/>
    public string FileExtension => ".csv";

    /// <inheritdoc/>
    public async Task WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken cancellationToken = default)
    {
        await using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

        // Headers
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                await writer.WriteAsync(Separator).ConfigureAwait(false);
            }

            WriteField(writer, fields[i].Header ?? fields[i].PropertyPath);
        }

        await writer.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken).ConfigureAwait(false);

        // Data rows
        await foreach (IReadOnlyDictionary<string, object?> data in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    await writer.WriteAsync(Separator).ConfigureAwait(false);
                }

                object? value = data.GetValueOrDefault(fields[i].PropertyPath);
                WriteField(writer, FormatValue(value, fields[i]));
            }

            await writer.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatValue(object? value, ExportFieldDescriptor field) =>
        value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString(field.Format ?? "dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(field.Format ?? "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString(field.Format ?? "dd/MM/yyyy", CultureInfo.InvariantCulture),
            decimal m => m.ToString(field.Format, CultureInfo.InvariantCulture),
            double d => d.ToString(field.Format, CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty,
        };

    private static readonly char[] FormulaTriggerChars = ['=', '+', '-', '@', '\t', '\r'];

    private static void WriteField(StreamWriter writer, string value)
    {
        bool needsFormulaProtection = value.Length > 0 && FormulaTriggerChars.Contains(value[0]);

        if (needsFormulaProtection || value.Contains(Separator) || value.Contains(Quote) || value.Contains('\n') || value.Contains('\r'))
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
