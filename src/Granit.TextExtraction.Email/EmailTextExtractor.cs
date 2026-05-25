using System.Globalization;
using System.Text;
using Granit.Html;
using Granit.Html.AngleSharp;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Cryptography;

namespace Granit.TextExtraction.Email;

/// <summary>
/// Extractor for <c>message/rfc822</c> (and the <c>application/eml</c> alias some
/// clients emit). Emits a structured header summary
/// (<c>Subject</c> / <c>From</c> / <c>To</c> / <c>Cc</c> / <c>Date</c>) followed by
/// the body text — preferring the plain-text alternative when present, falling back
/// to the HTML alternative routed through a SSRF-safe
/// <see cref="IHtmlToPlainTextConverter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Attachments are intentionally out of scope: an envelope alone covers the bulk of
/// search relevance for email, and the attachment payloads — if persisted to blob
/// storage — are picked up by their own per-format extractors downstream.
/// </para>
/// <para>
/// <c>S/MIME</c> and <c>PGP</c> encrypted bodies degrade to a literal
/// <c>[encrypted message]</c> placeholder. No decryption is attempted; the
/// extractor is a search-indexer, not a mail user agent.
/// </para>
/// <para>
/// Header values are sanitised (control characters dropped, except <c>\n</c> and
/// <c>\t</c>) to prevent log/header injection through indexed content. The body
/// is concatenated character-by-character against <c>maxCharLength</c> — the
/// extractor never emits more characters than the cap and signals truncation
/// via <see cref="TextExtractionResult.IsTruncated"/>.
/// </para>
/// </remarks>
public sealed partial class EmailTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.email";

    private const string Rfc822 = "message/rfc822";
    private const string EmlAlt = "application/eml";
    private const string EncryptedPlaceholder = "[encrypted message]";

    private readonly GranitTextExtractionOptions _options;
    private readonly ILogger<EmailTextExtractor> _logger;

    // The DI-registered converter ships with the trusted-templates profile so that
    // Notifications.Email keeps its historical behaviour. Email bodies are NOT trusted
    // content — they originate from outside the host — so we build our own untrusted
    // converter here. Same posture as Granit.TextExtraction.Text.HtmlTextExtractor.
#pragma warning disable CA1859
    private readonly IHtmlToPlainTextConverter _htmlConverter;
#pragma warning restore CA1859

    public EmailTextExtractor(
        IOptions<GranitTextExtractionOptions> options,
        ILogger<EmailTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
        _htmlConverter = new AngleSharpHtmlToPlainTextConverter(
            AngleSharpConfiguration.BuildForUntrustedContent());
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

        return contentType.Equals(Rfc822, StringComparison.OrdinalIgnoreCase)
            || contentType.Equals(EmlAlt, StringComparison.OrdinalIgnoreCase);
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

        // Wrap before any MimeKit allocation — VULN-001. persistent:false makes MimeKit
        // copy the message into memory eagerly so the wrapped stream is fully consumed
        // up-front, keeping the size cap deterministic.
        LimitedStream limited = new(source, _options.MaxBodySizeBytes);

        MimeMessage message;
        try
        {
            message = await MimeMessage.LoadAsync(limited, persistent: false, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            LogMalformedEmlSkipped(ex);
            return Skipped();
        }
        catch (EndOfStreamException ex)
        {
            LogMalformedEmlSkipped(ex);
            return Skipped();
        }

        StringBuilder sb = new(Math.Min(maxCharLength, 8192));
        bool truncated = false;

        AppendHeaders(message, sb, maxCharLength, ref truncated);

        if (!truncated)
        {
            AppendCapped(sb, "\n", maxCharLength, ref truncated);
        }

        if (!truncated)
        {
            if (message.Body is MultipartEncrypted)
            {
                AppendCapped(sb, EncryptedPlaceholder, maxCharLength, ref truncated);
            }
            else
            {
                string body = await ResolveBodyAsync(message, cancellationToken).ConfigureAwait(false);
                AppendCapped(sb, body, maxCharLength, ref truncated);
            }
        }

        string content = sb.ToString();
        return new TextExtractionResult(
            Content: content,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: content.Length,
            ExtractorName: ExtractorName);
    }

    private static void AppendHeaders(MimeMessage m, StringBuilder sb, int max, ref bool truncated)
    {
        AppendHeader(sb, "Subject", m.Subject ?? string.Empty, max, ref truncated);
        if (truncated)
        {
            return;
        }

        AppendHeader(sb, "From", FormatAddresses(m.From), max, ref truncated);
        if (truncated)
        {
            return;
        }

        AppendHeader(sb, "To", FormatAddresses(m.To), max, ref truncated);
        if (truncated)
        {
            return;
        }

        if (m.Cc.Count > 0)
        {
            AppendHeader(sb, "Cc", FormatAddresses(m.Cc), max, ref truncated);
            if (truncated)
            {
                return;
            }
        }

        AppendHeader(
            sb,
            "Date",
            m.Date.UtcDateTime.ToString("R", CultureInfo.InvariantCulture),
            max,
            ref truncated);
    }

    private static void AppendHeader(StringBuilder sb, string name, string value, int max, ref bool truncated)
    {
        AppendCapped(sb, name, max, ref truncated);
        if (truncated)
        {
            return;
        }

        AppendCapped(sb, ": ", max, ref truncated);
        if (truncated)
        {
            return;
        }

        AppendCapped(sb, Sanitize(value), max, ref truncated);
        if (truncated)
        {
            return;
        }

        AppendCapped(sb, "\n", max, ref truncated);
    }

    private static string FormatAddresses(InternetAddressList list) =>
        list.Count == 0 ? string.Empty : string.Join(", ", list.Select(a => a.ToString()));

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder buf = new(value.Length);
        foreach (char c in value)
        {
            // Strip control characters except line feed and tab — defends against
            // header/log injection through indexed content (e.g. an attacker crafting
            // a Subject with embedded CR/LF to splice fake log lines downstream).
            if (c < 0x20 && c != '\n' && c != '\t')
            {
                continue;
            }
            buf.Append(c);
        }
        return buf.ToString();
    }

    private async Task<string> ResolveBodyAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.TextBody))
        {
            return message.TextBody;
        }

        string? html = message.HtmlBody;
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return await _htmlConverter.ConvertAsync(html, cancellationToken).ConfigureAwait(false);
    }

    private static void AppendCapped(StringBuilder sb, string value, int max, ref bool truncated)
    {
        if (value.Length == 0)
        {
            return;
        }

        int remaining = max - sb.Length;
        if (remaining <= 0)
        {
            truncated = true;
            return;
        }

        if (value.Length > remaining)
        {
            sb.Append(value, 0, remaining);
            truncated = true;
            return;
        }

        sb.Append(value);
    }

    private static TextExtractionResult Skipped() =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: ExtractorName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "EmailTextExtractor skipped a malformed .eml message.")]
    private partial void LogMalformedEmlSkipped(Exception exception);
}
