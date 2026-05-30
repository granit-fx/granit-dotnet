namespace Granit.AI;

/// <summary>
/// Turns a prompt into a typed result via an LLM — the canonical structured-output
/// primitive for the framework (ADR-064).
/// </summary>
/// <remarks>
/// One injected service serves every result type (per-call generic). The implementation
/// pins the output with a provider-enforced JSON schema when the workspace's model
/// advertises <see cref="AIModelCapabilities.StructuredOutput"/>, and otherwise falls back
/// to injecting the schema into the prompt and stripping Markdown fences — so consumers
/// never re-derive that plumbing. Untrusted content is sanitized and delimited; the
/// developer-controlled instruction is not. Usage is tracked, the quota guard is applied,
/// and errors are mapped to a PII-safe <see cref="StructuredCompletionResult{T}"/>.
/// </remarks>
public interface IStructuredCompletion
{
    /// <summary>
    /// Completes <paramref name="request"/> into a typed <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The structured result type.</typeparam>
    /// <param name="request">The instruction, untrusted content, and optional context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result carrying the typed value (when <see cref="StructuredCompletionStatus.Succeeded"/>),
    /// the model id, a four-valued status, and response provenance.
    /// </returns>
    Task<StructuredCompletionResult<T>> CompleteAsync<T>(
        StructuredCompletionRequest request,
        CancellationToken cancellationToken = default)
        where T : class;
}
