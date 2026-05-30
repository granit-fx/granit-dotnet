namespace Granit.AI.Extraction;

/// <summary>
/// Describes a structured-data extraction or generation request, separating the trusted,
/// developer-controlled task instruction from the untrusted content that must be sanitised and
/// delimited before reaching the model (OWASP LLM01).
/// </summary>
public sealed record ExtractionRequest
{
    /// <summary>
    /// Gets the trusted, developer-controlled task instruction. This is never sanitised, so it must
    /// originate from code or a template — never from untrusted input. When <see langword="null"/> or
    /// blank, the extractor falls back to its generic structured-extraction instruction.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// Gets the untrusted content the model should reason over. The extractor sanitises it and wraps it
    /// in <c>&lt;data&gt;</c> delimiters before sending it to the model.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the trusted label describing the <see cref="Content"/> block (e.g. "Page content").
    /// Defaults to "Document" when <see langword="null"/> or blank.
    /// </summary>
    public string? ContentLabel { get; init; }

    /// <summary>
    /// Gets additional named, untrusted context sections (e.g. title, locale). Each value is sanitised
    /// and delimited by the extractor before being sent to the model.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string?>>? Context { get; init; }
}
