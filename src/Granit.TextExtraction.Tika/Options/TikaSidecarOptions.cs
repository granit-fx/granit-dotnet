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
    /// for <c>granit-tika</c> is wired with a custom primary message handler (typically
    /// configured by the host via <c>.ConfigurePrimaryHttpMessageHandler(...)</c> to attach
    /// a client certificate or a mesh-aware handler). Defaults to <c>true</c> in production;
    /// hosts opt down for dev/staging by setting <c>false</c> in
    /// <c>appsettings.Development.json</c>. Enforced at startup by the validator chain.
    /// </summary>
    public bool RequireMutualTls { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, <see cref="Uri"/> must use the <c>https</c> scheme. Defaults to
    /// <c>true</c>: document bytes (PII / contracts / medical records) must not leave the
    /// host in cleartext on the cluster network (GDPR Art. 32). The validator additionally
    /// allows <c>http://</c> when the URI host resolves to <c>localhost</c>, <c>127.0.0.1</c>,
    /// or <c>::1</c> so localhost dev sidecars stay frictionless.
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the extractor sends <c>X-Tika-Skip-Embedded-Resources: true</c>
    /// on every <c>PUT /tika</c> call so the sidecar does not recurse into embedded
    /// resources (Office linked content, mbox attachments, EPUB items). Closes a class of
    /// Tika SSRF / fetch-recursion CVEs historically associated with the embedded parser
    /// path. Defaults to <c>true</c>; opt down only when the host explicitly wants the
    /// recursive index.
    /// </summary>
    public bool SkipEmbeddedResources { get; set; } = true;

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
