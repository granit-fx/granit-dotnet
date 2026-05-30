namespace Granit.AI;

/// <summary>
/// Outcome of a <see cref="IStructuredCompletion"/> call. Four-valued so a caller can
/// map distinct failure modes onto its own domain status (ADR-064).
/// </summary>
public enum StructuredCompletionStatus
{
    /// <summary>A typed value was produced.</summary>
    Succeeded,

    /// <summary>The model declined or returned no content (e.g. a safety refusal or empty answer).</summary>
    ModelRefused,

    /// <summary>The model produced content that did not match the expected schema / failed to deserialize.</summary>
    SchemaViolation,

    /// <summary>The call failed before a usable response (timeout, provider, or network error).</summary>
    TransportFailure,
}
