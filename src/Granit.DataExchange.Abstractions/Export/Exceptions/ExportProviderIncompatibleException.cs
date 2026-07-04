namespace Granit.DataExchange.Export.Exceptions;

/// <summary>
/// Thrown when the chosen export format does not support complex/hierarchical fields
/// declared in the export definition and the policy is <see cref="OnIncompatibleFieldPolicy.Throw"/>.
/// </summary>
public sealed class ExportProviderIncompatibleException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="ExportProviderIncompatibleException"/>.
    /// </summary>
    /// <param name="format">The export format that lacks hierarchy support (e.g. <c>"csv"</c>).</param>
    /// <param name="complexFieldCount">Number of complex fields that require hierarchy support.</param>
    public ExportProviderIncompatibleException(string format, int complexFieldCount)
        : base(
            $"Export format '{format}' does not support hierarchical fields " +
            "(SupportsHierarchy = false). " +
            $"The export definition declares {complexFieldCount} complex field(s) that require a structured writer. " +
            "Use a structured format (e.g. 'json', 'xml') or set OnIncompatibleField = Skip " +
            "to export only the scalar fields.")
    {
        Format = format;
        ComplexFieldCount = complexFieldCount;
    }

    /// <summary>The export format that lacks hierarchy support.</summary>
    public string Format { get; }

    /// <summary>Number of complex fields that triggered this exception.</summary>
    public int ComplexFieldCount { get; }
}
