using Granit.Html;
using Granit.Html.AngleSharp;
using Granit.TextExtraction.Exceptions;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Text;

/// <summary>
/// Extractor for <c>text/html</c> and <c>application/xhtml+xml</c>. Delegates to
/// <see cref="IHtmlToPlainTextConverter"/> for the DOM walk and honours the
/// truncation contract by trimming the resulting plain text to <c>maxCharLength</c>.
/// </summary>
/// <remarks>
/// SSRF posture: the extractor constructs its own <see cref="IHtmlToPlainTextConverter"/>
/// using <see cref="AngleSharpConfiguration.BuildForUntrustedContent"/> by default. When the
/// host sets <see cref="GranitTextExtractionOptions.ResolveHtmlExternalResources"/> to
/// <c>true</c> — only appropriate for fully trusted corpora — the converter is rebuilt with
/// <see cref="AngleSharpConfiguration.BuildForTrustedTemplates"/>. The DI-registered
/// <see cref="IHtmlToPlainTextConverter"/> is intentionally NOT consumed because it ships
/// with the trusted-templates profile to keep Notifications.Email behaviour intact.
/// </remarks>
public sealed class HtmlTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.html";

    private readonly GranitTextExtractionOptions _options;

    // Typed as the abstraction even though only AngleSharp ships today — keeps the
    // contract surface symmetric with the rest of Granit.TextExtraction.* and lets
    // downstream tests / forks swap in a different impl without changing this class.
    // CA1859 prefers the concrete type for a perf nudge; one virtual call per
    // extraction is dwarfed by the AngleSharp parse it brackets.
#pragma warning disable CA1859
    private readonly IHtmlToPlainTextConverter _converter;
#pragma warning restore CA1859

    public HtmlTextExtractor(IOptions<GranitTextExtractionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _converter = new AngleSharpHtmlToPlainTextConverter(
            _options.ResolveHtmlExternalResources
                ? AngleSharpConfiguration.BuildForTrustedTemplates()
                : AngleSharpConfiguration.BuildForUntrustedContent());
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        // Wrap the source for VULN-001 protection before feeding it to AngleSharp.
        // AngleSharp reads the full HTML into a string internally, so we materialise here.
        LimitedStream limited = new(source, _options.MaxBodySizeBytes);

        string html;
        using (StreamReader reader = new(limited, leaveOpen: true))
        {
            try
            {
                html = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TextExtractionException)
            {
                throw;
            }
        }

        string plain = await _converter.ConvertAsync(html, cancellationToken).ConfigureAwait(false);

        bool truncated = false;
        if (plain.Length > maxCharLength)
        {
            plain = plain[..maxCharLength];
            truncated = true;
        }

        return new TextExtractionResult(
            Content: plain,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: plain.Length,
            ExtractorName: ExtractorName);
    }
}
