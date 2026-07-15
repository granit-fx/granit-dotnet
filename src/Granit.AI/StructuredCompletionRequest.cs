using Microsoft.Extensions.AI;

namespace Granit.AI;

/// <summary>
/// Describes a structured-completion request for <see cref="IStructuredCompletion"/>.
/// </summary>
/// <remarks>
/// Separates the developer-controlled task <see cref="Instruction"/> from the untrusted
/// <see cref="Content"/> (and optional <see cref="Context"/>) so the implementation can
/// sanitize and delimit everything that originates outside the application — the
/// framework's first line of defence against OWASP LLM01 prompt injection. Binary
/// <see cref="Attachments"/> enable multimodal requests (image analysis, vision OCR);
/// at least one of <see cref="Content"/> or <see cref="Attachments"/> must be supplied.
/// </remarks>
public sealed record StructuredCompletionRequest
{
    /// <summary>
    /// Developer-controlled task instruction (e.g. a per-locale generation prompt). When
    /// <c>null</c> or blank, a generic structured-output instruction is used.
    /// </summary>
    /// <remarks>
    /// NEVER sanitized — appended verbatim to the prompt. Supply only code- or
    /// template-controlled text; routing untrusted input through this member would defeat
    /// the prompt-injection isolation applied to <see cref="Content"/>.
    /// </remarks>
    public string? Instruction { get; init; }

    /// <summary>
    /// The untrusted text to analyze, or <c>null</c> for attachment-only (e.g. image-only)
    /// requests. Sanitized and wrapped in a <c>&lt;data&gt;</c> block by the implementation
    /// before it reaches the model.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Developer-controlled label for the <see cref="Content"/> block. Defaults to
    /// <c>"Document"</c> when <c>null</c>.
    /// </summary>
    public string? ContentLabel { get; init; }

    /// <summary>
    /// Optional additional named untrusted sections (title, locale, …). Each entry is
    /// sanitized and delimited. The keys are developer-controlled labels; the values are
    /// treated as untrusted.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string?>>? Context { get; init; }

    /// <summary>
    /// Optional binary parts (images, …) appended to the user message after the prompt,
    /// enabling multimodal structured completion against a vision-capable model.
    /// </summary>
    /// <remarks>
    /// TRUSTED-BY-CALLER: unlike <see cref="Content"/>, binary parts cannot flow through the
    /// text sanitization envelope — the caller owns size caps and provenance checks (e.g.
    /// <c>Granit.Imaging.AI</c> enforces <c>MaxImageBytes</c> and a content-type allowlist).
    /// Constrained to <see cref="DataContent"/> so untrusted text can never be smuggled past
    /// the <c>&lt;data&gt;</c> envelope as a raw message part.
    /// </remarks>
    public IReadOnlyList<DataContent>? Attachments { get; init; }

    /// <summary>
    /// Workspace to run against. When <c>null</c>, the configured default workspace is used.
    /// </summary>
    public string? WorkspaceName { get; init; }
}
