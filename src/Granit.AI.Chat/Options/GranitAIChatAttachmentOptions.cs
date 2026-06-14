namespace Granit.AI.Chat.Options;

/// <summary>
/// Limits for chat file attachments (ADR-067), bound from the <c>AI:Chat:Attachments</c>
/// configuration section. Enforced both at the endpoint (request validation) and during
/// server-side resolution (defence in depth).
/// </summary>
public sealed class GranitAIChatAttachmentOptions
{
    /// <summary>The configuration section bound to these options.</summary>
    public const string SectionName = "AI:Chat:Attachments";

    /// <summary>Maximum number of attachments on a single turn. Default 5.</summary>
    public int MaxAttachments { get; set; } = 5;

    /// <summary>Maximum size, in bytes, of a single attachment. Default 10 MiB.</summary>
    public long MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// The content types the text-extraction path accepts. An attachment whose declared content
    /// type is not listed is rejected. Defaults cover the formats the bundled
    /// <c>Granit.TextExtraction.*</c> extractors handle.
    /// </summary>
    public HashSet<string> AllowedContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/markdown",
        "text/html",
        "text/csv",
        "application/json",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "message/rfc822",
    };
}
