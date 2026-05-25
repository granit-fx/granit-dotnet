using System.ComponentModel.DataAnnotations;

namespace Granit.TextExtraction.Tika.Options;

/// <summary>
/// Configuration options for the Tika sidecar text extractor. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class TikaSidecarOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction:Tika";

    /// <summary>
    /// Sidecar endpoint base URI (e.g. <c>http://tika:9998</c> or
    /// <c>https://tika.internal/tika</c>). The extractor POSTs the document bytes to
    /// <c>{Uri}/tika</c> and reads back plain text.
    /// </summary>
    [Required]
    public Uri Uri { get; set; } = null!;

    /// <summary>Per-call HTTP timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Allowlist of hostnames the extractor is allowed to talk to. Must be non-empty
    /// — the module refuses to start otherwise. The host segment of <see cref="Uri"/>
    /// must be present in this list. No default of <c>["*"]</c>: explicit-only.
    /// </summary>
    public IList<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// When <c>true</c>, the module refuses to start unless the named HTTP client
    /// for <c>granit-tika</c> is wired with a primary message handler (typically
    /// configured by the host to attach a client certificate). Defaults to
    /// <c>true</c> in production; the host opts down for dev/staging.
    /// </summary>
    public bool RequireMutualTls { get; set; } = true;

    /// <summary>
    /// MIME types the extractor will claim via <see cref="ITextExtractor.CanHandle"/>.
    /// Defaults to a curated list of formats not covered by the TE-F2 native
    /// extractors (RTF, ODF family, mailboxes, common archives). Override to extend.
    /// </summary>
    public IList<string> AllowedContentTypes { get; set; } =
    [
        "application/rtf",
        "text/rtf",
        "application/vnd.oasis.opendocument.text",         // .odt
        "application/vnd.oasis.opendocument.spreadsheet",  // .ods
        "application/vnd.oasis.opendocument.presentation", // .odp
        "message/rfc822",                                  // .eml
        "application/mbox",                                // mailbox archive
        "application/epub+zip",                            // .epub
    ];
}
