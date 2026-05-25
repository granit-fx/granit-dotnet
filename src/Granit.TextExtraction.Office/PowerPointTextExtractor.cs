using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Granit.TextExtraction.Office.Internal;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Office;

/// <summary>
/// Extractor for
/// <c>application/vnd.openxmlformats-officedocument.presentationml.presentation</c>
/// (.pptx). Iterates each <see cref="SlidePart"/>, takes its
/// <c>Slide.InnerText</c>, and joins slides with blank lines.
/// </summary>
public sealed partial class PowerPointTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.powerpoint";

    private const string Pptx =
        "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private readonly GranitTextExtractionOptions _options;
    private readonly ILogger<PowerPointTextExtractor> _logger;

    public PowerPointTextExtractor(
        IOptions<GranitTextExtractionOptions> options,
        ILogger<PowerPointTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.Equals(Pptx, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        byte[] bytes = await OpenXmlExtraction
            .ReadAllBytesAsync(source, _options.MaxBodySizeBytes, cancellationToken)
            .ConfigureAwait(false);

        OpenXmlGate.GateResult gate = OpenXmlGate.Inspect(bytes, _options);
        if (gate != OpenXmlGate.GateResult.Ok)
        {
            LogPackageRejected(gate.ToString());
            return OpenXmlExtraction.Skipped(ExtractorName);
        }

        try
        {
            using MemoryStream pkg = new(bytes, writable: false);
            using var doc = PresentationDocument.Open(
                pkg, isEditable: false, OpenXmlExtraction.BuildOpenSettings(_options));

            PresentationPart? presentationPart = doc.PresentationPart;
            if (presentationPart is null)
            {
                return OpenXmlExtraction.Truncate(string.Empty, maxCharLength, ExtractorName);
            }

            StringBuilder sb = new(Math.Min(maxCharLength, 8192));
            bool truncated = false;
            bool first = true;

            foreach (SlidePart slidePart in presentationPart.SlideParts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!first)
                {
                    // Slide separator: blank line.
                    int remaining = maxCharLength - sb.Length;
                    if (remaining < 2)
                    {
                        truncated = true;
                        break;
                    }
                    sb.Append('\n');
                    sb.Append('\n');
                }

                string slideText = slidePart.Slide?.InnerText ?? string.Empty;
                int room = maxCharLength - sb.Length;

                if (slideText.Length <= room)
                {
                    sb.Append(slideText);
                }
                else
                {
                    sb.Append(slideText, 0, Math.Max(room, 0));
                    truncated = true;
                    break;
                }

                first = false;
            }

            return new TextExtractionResult(
                Content: sb.ToString(),
                DetectedLanguage: null,
                IsTruncated: truncated,
                CharCount: sb.Length,
                ExtractorName: ExtractorName);
        }
        catch (Exception ex) when (IsOpenXmlException(ex))
        {
            LogPackageParseFailed(ex);
            return OpenXmlExtraction.Skipped(ExtractorName);
        }
    }

    private static bool IsOpenXmlException(Exception ex) =>
        ex.GetType().Namespace?.StartsWith("DocumentFormat.OpenXml", StringComparison.Ordinal) == true
        || ex is FileFormatException
        || ex is InvalidDataException;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PowerPointTextExtractor rejected a .pptx package: {Reason}.")]
    private partial void LogPackageRejected(string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PowerPointTextExtractor failed to parse a .pptx package.")]
    private partial void LogPackageParseFailed(Exception exception);
}
