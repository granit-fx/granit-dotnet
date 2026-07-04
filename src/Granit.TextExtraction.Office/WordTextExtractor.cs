using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Granit.TextExtraction.Office.Internal;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Office;

/// <summary>
/// Extractor for <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c>
/// (.docx). Reads <c>MainDocumentPart.Document.Body.InnerText</c> via OpenXml.
/// </summary>
public sealed partial class WordTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.word";

    private const string Docx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly GranitTextExtractionOptions _options;
    private readonly ILogger<WordTextExtractor> _logger;

    public WordTextExtractor(IOptions<GranitTextExtractionOptions> options, ILogger<WordTextExtractor> logger)
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
        && contentType.Equals(Docx, StringComparison.OrdinalIgnoreCase);

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
            await using MemoryStream pkg = new(bytes, writable: false);
            using var doc = WordprocessingDocument.Open(
                pkg, isEditable: false, OpenXmlExtraction.BuildOpenSettings(_options));

            Body? body = doc.MainDocumentPart?.Document?.Body;
            string text = body?.InnerText ?? string.Empty;

            return OpenXmlExtraction.Truncate(text, maxCharLength, ExtractorName);
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
        Message = "WordTextExtractor rejected a .docx package: {Reason}.")]
    private partial void LogPackageRejected(string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WordTextExtractor failed to parse a .docx package.")]
    private partial void LogPackageParseFailed(Exception exception);
}
