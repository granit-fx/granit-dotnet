namespace Granit.AI.Redaction;

/// <summary>
/// Default <see cref="IAIContentRedactor"/>: identity. Returns content as-is.
/// </summary>
/// <remarks>
/// Shipped as the framework default because no regex-based PII pattern set is generic
/// enough to be safe across all corpora — see remarks on <see cref="IAIContentRedactor"/>.
/// Hosts that need redaction register a stricter impl (NER, regex tuned to their domain,
/// composite) <i>before</i> calling the AI-feature <c>Add…</c> extensions.
/// </remarks>
public sealed class NoOpAIContentRedactor : IAIContentRedactor
{
    /// <inheritdoc/>
    public string Redact(string content) => content;
}
