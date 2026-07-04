namespace Granit.DocumentGeneration.Pdf.PdfA;

/// <summary>
/// Options for PDF/A conversion and Factur-X compliance.
/// </summary>
public sealed class PdfAConversionOptions
{
    /// <summary>
    /// The PDF/A conformance level. Default is <see cref="PdfAConformanceLevel.PdfA3b"/>.
    /// </summary>
    public PdfAConformanceLevel ConformanceLevel { get; init; } = PdfAConformanceLevel.PdfA3b;

    /// <summary>
    /// Optional Factur-X / ZUGFeRD XML content to embed as an attachment.
    /// When set, the resulting PDF/A-3b will contain the XML as an associated file
    /// per the Factur-X / ZUGFeRD specification (EN 16931).
    /// </summary>
    public string? FacturXXmlContent { get; init; }

    /// <summary>
    /// The Factur-X conformance level (e.g. "EN 16931", "MINIMUM", "BASIC").
    /// Only relevant when <see cref="FacturXXmlContent"/> is provided.
    /// </summary>
    public string? FacturXConformanceLevel { get; init; }

    /// <summary>
    /// Document title for XMP metadata.
    /// </summary>
    public string? DocumentTitle { get; init; }

    /// <summary>
    /// Document author for XMP metadata.
    /// </summary>
    public string? DocumentAuthor { get; init; }
}

/// <summary>
/// PDF/A conformance levels supported by the converter.
/// </summary>
public enum PdfAConformanceLevel
{
    /// <summary>PDF/A-3b — visual appearance preserved, embedded files allowed. Required for Factur-X.</summary>
    PdfA3b,

    /// <summary>PDF/A-2a — tagged PDF with accessibility support. Suitable for medical document archival.</summary>
    PdfA2a,
}
