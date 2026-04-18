namespace Granit.DataExchange.Export;

/// <summary>
/// Fluent builder for configuring how a single field is exported.
/// </summary>
public sealed class ExportFieldBuilder
{
    internal string? HeaderValue { get; private set; }
    internal string? FormatValue { get; private set; }
    internal int OrderValue { get; private set; }

    /// <summary>
    /// Sets the column header name in the exported file.
    /// If not set, the property path is used.
    /// </summary>
    public ExportFieldBuilder Header(string header)
    {
        HeaderValue = header;
        return this;
    }

    /// <summary>
    /// Sets the display format for this field (e.g. <c>"dd/MM/yyyy"</c> for dates,
    /// <c>"#,##0.00"</c> for numbers).
    /// </summary>
    public ExportFieldBuilder Format(string format)
    {
        FormatValue = format;
        return this;
    }

    /// <summary>
    /// Sets the display order for this field. Lower values appear first.
    /// </summary>
    public ExportFieldBuilder Order(int order)
    {
        OrderValue = order;
        return this;
    }
}
