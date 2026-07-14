namespace Granit.DataExchange.Export;

/// <summary>
/// Writes export data to a specific file format (Excel, CSV, JSON, XML, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered as singletons via the format-specific packages
/// (<c>Granit.DataExchange.Excel</c>, <c>Granit.DataExchange.Csv</c>,
/// <c>Granit.DataExchange.Json</c>, <c>Granit.DataExchange.Xml</c>).
/// </para>
/// <para>
/// The writer receives a stream of ordered row arrays — each value aligned to the index of its
/// <see cref="ExportFieldDescriptor"/> in the field list — and writes them sequentially.
/// For large datasets, the writer should process rows in a streaming fashion to minimize
/// memory usage.
/// </para>
/// </remarks>
public interface IExportWriter
{
    /// <summary>
    /// Whether this writer supports the given format.
    /// </summary>
    /// <param name="format">The format identifier (e.g. <c>"xlsx"</c>, <c>"csv"</c>, <c>"json"</c>).</param>
    bool CanWrite(string format);

    /// <summary>
    /// MIME type for the output file (e.g. <c>"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"</c>).
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// File extension including the dot (e.g. <c>".xlsx"</c>, <c>".csv"</c>, <c>".json"</c>).
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Describes what this writer can handle beyond basic scalar rows.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="ExportFormatCapabilities.TabularOnly"/> (no hierarchy support).
    /// Structured writers (JSON, XML) override this to <see cref="ExportFormatCapabilities.Structured"/>.
    /// The orchestrator checks this before executing a definition that contains complex fields.
    /// </remarks>
    ExportFormatCapabilities Capabilities => ExportFormatCapabilities.TabularOnly;

    /// <summary>
    /// Writes export data to the output stream.
    /// </summary>
    /// <param name="output">The target stream.</param>
    /// <param name="fields">Ordered field descriptors (defines columns).</param>
    /// <param name="rows">
    /// Streaming row data. Each array holds the field values in the exact order of
    /// <paramref name="fields"/> (value at index <c>i</c> belongs to <c>fields[i]</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of data rows written (excluding headers).</returns>
    Task<long> WriteAsync(
        Stream output,
        IReadOnlyList<ExportFieldDescriptor> fields,
        IAsyncEnumerable<object?[]> rows,
        CancellationToken cancellationToken = default);
}
