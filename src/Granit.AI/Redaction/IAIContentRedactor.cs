namespace Granit.AI.Redaction;

/// <summary>
/// Seam that wraps free-text content before it is sent to an LLM, redacting or
/// masking PII the host considers sensitive. Returns the content unchanged when no
/// pattern matches — the contract is "best-effort, never throw".
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a seam.</b> Free-text PII detection is host-specific: GDPR-grade hosts wire
/// in NER (spaCy, presidio, …), low-risk hosts ship the no-op default, regulated
/// environments may chain multiple redactors. The framework deliberately does not ship
/// regex-based defaults — half-baked patterns leak more than they redact and create a
/// false sense of compliance.
/// </para>
/// <para>
/// <b>Where it runs.</b> Callers (AI language detector, AI summarizer, future AI
/// auto-tagger) invoke <see cref="Redact"/> on every outbound content payload before
/// the LLM call when their option <c>RedactPIIBeforeLLMCall</c> is <c>true</c>
/// (default in all packages).
/// </para>
/// </remarks>
public interface IAIContentRedactor
{
    /// <summary>
    /// Returns <paramref name="content"/> with sensitive substrings masked. MUST be
    /// pure (no I/O), MUST NOT throw — return <paramref name="content"/> unchanged on
    /// pattern failure.
    /// </summary>
    string Redact(string content);
}
