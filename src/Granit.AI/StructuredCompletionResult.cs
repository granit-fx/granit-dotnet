using Microsoft.Extensions.AI;

namespace Granit.AI;

/// <summary>
/// Result of a <see cref="IStructuredCompletion"/> call: the typed value, model provenance,
/// a four-valued status, and the response metadata layered scorers need.
/// </summary>
/// <typeparam name="T">The structured result type.</typeparam>
public sealed record StructuredCompletionResult<T> where T : class
{
    /// <summary>Outcome of the call.</summary>
    public required StructuredCompletionStatus Status { get; init; }

    /// <summary>
    /// The typed value. Non-<c>null</c> only when <see cref="Status"/> is
    /// <see cref="StructuredCompletionStatus.Succeeded"/>.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>Identifier of the model that produced the result, as reported by the provider.</summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// PII-safe failure description when <see cref="Status"/> is not
    /// <see cref="StructuredCompletionStatus.Succeeded"/>. Never echoes the prompt payload
    /// or a provider message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Why the model stopped generating, when reported. Surfaced so a layer (e.g. an
    /// Extraction confidence estimate) can score without re-issuing the call.
    /// </summary>
    public ChatFinishReason? FinishReason { get; init; }

    /// <summary>
    /// Provider-supplied response metadata (the MEAI <c>AdditionalProperties</c> bag), when
    /// present. A scorer may read provider-specific signals such as a <c>"confidence"</c> value.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>Token usage for the call, when the provider reports it.</summary>
    public UsageDetails? Usage { get; init; }
}
