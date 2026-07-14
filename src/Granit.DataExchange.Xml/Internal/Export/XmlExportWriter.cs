using System.Collections.Concurrent;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using Granit.DataExchange.Export;

namespace Granit.DataExchange.Xml.Internal.Export;

/// <summary>
/// <see cref="IExportWriter"/> implementation that produces <c>.xml</c> files.
/// </summary>
/// <remarks>
/// <para>
/// Supports hierarchical/complex fields (<see cref="ExportFormatCapabilities.SupportsHierarchy"/> = <c>true</c>).
/// Complex field values are serialized as XML sub-trees using <see cref="XmlSerializer"/> with the
/// exact <c>TValue</c> type captured at definition time (<see cref="ExportFieldDescriptor.SelectorType"/>).
/// </para>
/// <para>
/// <b>Limitation:</b> <see cref="XmlSerializer"/> cannot serialize interface types (e.g. <c>IList&lt;T&gt;</c>)
/// or <c>IDictionary&lt;TKey, TValue&gt;</c> with non-string values. Use concrete, serializable types
/// for complex fields, or use the JSON writer for polymorphic/dictionary payloads.
/// </para>
/// <para>
/// <see cref="XmlSerializer"/> instances are cached in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// to avoid per-row dynamic assembly generation overhead on large exports.
/// </para>
/// </remarks>
internal sealed class XmlExportWriter : IExportWriter
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> SerializerCache = new();

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Async = true,
        Encoding = System.Text.Encoding.UTF8,
        Indent = false,
        OmitXmlDeclaration = false,
    };

    /// <inheritdoc/>
    public bool CanWrite(string format) =>
        string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string MimeType => "application/xml";

    /// <inheritdoc/>
    public string FileExtension => ".xml";

    /// <inheritdoc/>
    public ExportFormatCapabilities Capabilities => ExportFormatCapabilities.Structured;

    /// <inheritdoc/>
    public async Task<long> WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<object?[]> rows,
        CancellationToken cancellationToken = default)
    {
        // Pre-warm serializer cache for complex fields so GetOrAdd never runs inside the row loop
        foreach (ExportFieldDescriptor field in fields.Where(f => f.RequiresHierarchy && f.SelectorType is not null))
        {
            GetOrCreateSerializer(field.SelectorType!);
        }

        await using var xmlWriter = XmlWriter.Create(output, WriterSettings);

        await xmlWriter.WriteStartDocumentAsync().ConfigureAwait(false);
        await xmlWriter.WriteStartElementAsync(null, "Export", null).ConfigureAwait(false);

        long rowCount = 0;
        await foreach (object?[] row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await xmlWriter.WriteStartElementAsync(null, "Row", null).ConfigureAwait(false);

            for (int i = 0; i < fields.Count; i++)
            {
                ExportFieldDescriptor field = fields[i];
                string elementName = field.PropertyPath.Replace('.', '_');
                object? value = row[i];

                await xmlWriter.WriteStartElementAsync(null, elementName, null).ConfigureAwait(false);

                if (value is not null)
                {
                    if (field.ValueSelector is not null)
                    {
                        WriteComplexValue(xmlWriter, value, field);
                    }
                    else
                    {
                        await xmlWriter.WriteStringAsync(FormatScalar(value, field)).ConfigureAwait(false);
                    }
                }

                await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
            }

            await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
            rowCount++;

            // Flush after each row to keep buffer bounded on large exports
            await xmlWriter.FlushAsync().ConfigureAwait(false);
        }

        await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
        await xmlWriter.WriteEndDocumentAsync().ConfigureAwait(false);
        await xmlWriter.FlushAsync().ConfigureAwait(false);
        return rowCount;
    }

    private static void WriteComplexValue(XmlWriter xmlWriter, object value, ExportFieldDescriptor field)
    {
        Type serializationType = field.SelectorType ?? value.GetType();
        XmlSerializer serializer = GetOrCreateSerializer(serializationType);

        // Serialize the complex value using a string-backed writer to avoid namespace pollution
        using System.IO.StringWriter sw = new();
        using var subWriter = XmlWriter.Create(sw, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
        });

        try
        {
            serializer.Serialize(subWriter, value);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is NotSupportedException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"XmlSerializer cannot serialize type '{serializationType.FullName}' " +
                $"for complex field '{field.PropertyPath}'. " +
                "Ensure the type is a concrete, public class with a parameterless constructor. " +
                "Interface types (IList<T>, IDictionary<TKey,TValue>) and abstract types are not supported by XmlSerializer. " +
                "Use the JSON writer (format 'json') for polymorphic or dictionary payloads.", ex);
        }

        // Write the serialized content inline (skip the root wrapper element)
        subWriter.Flush();
        string serialized = sw.ToString();

        // Parse and replay the inner content of the root element
        using System.IO.StringReader sr = new(serialized);
        using var reader = XmlReader.Create(sr);
        reader.MoveToContent();
        if (reader.IsEmptyElement)
        {
            return;
        }

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            xmlWriter.WriteNode(reader, defattr: true);
        }
    }

    private static string FormatScalar(object value, ExportFieldDescriptor field) =>
        value switch
        {
            DateTime dt => dt.ToString(field.Format ?? "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(field.Format ?? "yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString(field.Format ?? "yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal m => m.ToString(field.Format, CultureInfo.InvariantCulture),
            double d => d.ToString(field.Format, CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty,
        };

    private static XmlSerializer GetOrCreateSerializer(Type type) =>
        SerializerCache.GetOrAdd(type, static t =>
        {
            try
            {
                return new XmlSerializer(t);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create XmlSerializer for type '{t.FullName}'. " +
                    "XmlSerializer does not support interface types, abstract types, or types without a public parameterless constructor. " +
                    "Use the JSON writer (format 'json') for unsupported types.", ex);
            }
        });
}
