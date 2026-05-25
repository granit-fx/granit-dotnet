using DocumentFormat.OpenXml.Packaging;
using Granit.TextExtraction.Options;

namespace Granit.TextExtraction.Office.Internal;

/// <summary>
/// Shared helpers for the Word / Excel / PowerPoint extractors — input buffering,
/// gate enforcement, result shaping, and the per-part char-cap settings.
/// </summary>
internal static class OpenXmlExtraction
{
    public static OpenSettings BuildOpenSettings(GranitTextExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new OpenSettings
        {
            // Refuse XML parts larger than the configured output cap. The OpenXml parser
            // honours this by throwing FileFormatException so the host stays bounded.
            MaxCharactersInPart = options.MaxExtractedCharLength,
            AutoSave = false,
        };
    }

    public static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        LimitedStream limited = new(source, maxBytes);
        using MemoryStream buffer = new();
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
