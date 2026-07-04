using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Granit.TextExtraction.Options;

namespace Granit.TextExtraction.Office.Internal;

/// <summary>
/// Shared helpers for the Word / Excel / PowerPoint extractors — input buffering,
/// gate enforcement, result shaping, and the per-part char-cap settings.
/// </summary>
internal static class OpenXmlExtraction
{
    /// <remarks>
    /// We deliberately don't strip external relationships
    /// (<c>&lt;Relationship Target="http://..."&gt;</c>) from the <c>.rels</c> parts
    /// before opening: OpenXml's reader never dereferences an external relationship
    /// target on its own, so a strip pass would cost CPU on every document for zero
    /// behaviour change. The defence is structural instead — the architecture test
    /// <c>Office_assembly_must_not_reference_HTTP_or_external_URI_APIs</c> enforces
    /// that the Office assembly cannot reference <see cref="System.Net.Http.HttpClient"/> /
    /// <c>System.Net.WebRequest</c> / <c>System.Net.Sockets</c> at build time, so even
    /// a future code path that tried to follow an external target would fail to link.
    /// </remarks>
    public static OpenSettings BuildOpenSettings(GranitTextExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new OpenSettings
        {
            // Refuse XML parts larger than the configured output cap. The OpenXml parser
            // honours this by throwing FileFormatException so the host stays bounded.
            MaxCharactersInPart = options.MaxExtractedCharLength,
            AutoSave = false,
            // Pin Markup Compatibility processing to the Office 2010 schema set and
            // walk only loaded parts. Future Office versions introducing new MC choice
            // branches degrade gracefully (the parser ignores the unknown extension)
            // instead of triggering OpenXml's "best effort" fallback — which has a
            // history of opening attack surface for crafted alternate-content blocks.
            MarkupCompatibilityProcessSettings = new MarkupCompatibilityProcessSettings(
                MarkupCompatibilityProcessMode.ProcessLoadedPartsOnly,
                FileFormatVersions.Office2010),
        };
    }

    public static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        LimitedStream limited = new(source, maxBytes);
        await using MemoryStream buffer = new();
        await limited.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    public static TextExtractionResult Skipped(string extractorName) =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: extractorName,
            Confidence: ExtractionConfidence.Deterministic);

    public static TextExtractionResult Truncate(string content, int maxCharLength, string extractorName)
    {
        bool truncated = content.Length > maxCharLength;
        string output = truncated ? content[..maxCharLength] : content;

        return new TextExtractionResult(
            Content: output,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: output.Length,
            ExtractorName: extractorName,
            Confidence: ExtractionConfidence.Deterministic);
    }
}
