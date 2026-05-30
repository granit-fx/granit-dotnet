namespace Granit.AI.Extraction;

/// <summary>
/// Describes a structured-extraction (or typed-generation) request for
/// <see cref="IDocumentExtractor{TResult}"/>.
/// </summary>
/// <remarks>
/// The document-extraction flavour of <see cref="Granit.AI.StructuredCompletionRequest"/>:
/// separates the developer-controlled task <see cref="Instruction"/> from the untrusted
/// <see cref="Content"/> (and optional <see cref="Context"/>) so the extractor can sanitize
/// and delimit everything that originates outside the application. The instruction is the
/// only member never sanitized — it must come from code or a developer-authored template.
/// </remarks>
public sealed record ExtractionRequest
{
    /// <summary>
    /// Developer-controlled task instruction (e.g. a per-locale generation prompt). When
    /// <c>null</c> or blank, the extractor falls back to its generic extraction instruction.
    /// </summary>
    /// <remarks>
    /// NEVER sanitized — appended verbatim to the prompt. Supply only code- or
    /// template-controlled text here.
    /// </remarks>
    public string? Instruction { get; init; }

    /// <summary>
    /// The untrusted text to analyze. Sanitized and wrapped in a <c>&lt;data&gt;</c> block
    /// before it reaches the model.
    /// </summary>
    public required string Content { get; init; }

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
}
