using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.DataExchange.Export;

namespace Granit.DataExchange.Json.Internal.Export;

/// <summary>
/// <see cref="IExportWriter"/> implementation that produces <c>.json</c> files using
/// <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Supports hierarchical/complex fields (<see cref="ExportFormatCapabilities.SupportsHierarchy"/> = <c>true</c>).
/// Complex field values are serialized using the exact <c>TValue</c> type captured at definition time
/// (<see cref="ExportFieldDescriptor.SelectorType"/>), preserving the full object graph including
/// polymorphic sub-types.
/// </para>
/// <para>
/// The writer flushes its internal buffer after every row to keep memory bounded on large exports.
/// </para>
/// </remarks>
internal sealed class JsonExportWriter : IExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public bool CanWrite(string format) =>
        string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string MimeType => "application/json";

    /// <inheritdoc/>
    public string FileExtension => ".json";

    /// <inheritdoc/>
    public ExportFormatCapabilities Capabilities => ExportFormatCapabilities.Structured;

    /// <inheritdoc/>
    public async Task<long> WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<object?[]> rows,
        CancellationToken cancellationToken = default)
    {
        await using Utf8JsonWriter jsonWriter = new(output, new JsonWriterOptions { SkipValidation = false });

        jsonWriter.WriteStartArray();

        long rowCount = 0;
        await foreach (object?[] row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            jsonWriter.WriteStartObject();

            for (int i = 0; i < fields.Count; i++)
            {
                ExportFieldDescriptor field = fields[i];
                jsonWriter.WritePropertyName(field.PropertyPath);

                object? value = row[i];
                Type valueType = field.SelectorType ?? value?.GetType() ?? typeof(object);
                JsonSerializer.Serialize(jsonWriter, value, valueType, SerializerOptions);
            }

            jsonWriter.WriteEndObject();
            rowCount++;

            // Flush after each row to keep buffer bounded on large exports
            await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        jsonWriter.WriteEndArray();
        await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        return rowCount;
    }
}
