namespace Granit.AI.Tools;

/// <summary>
/// Drives the agentic loop (ADR-067): send the conversation to the model with the available
/// tool declarations, execute any tools the model requests, feed the results back, and repeat
/// until the model stops requesting tools — bounded by an iteration cap and per-result context
/// guards, and stamping usage on completion.
/// </summary>
public interface IAIToolOrchestrator
{
    /// <summary>
    /// Runs the loop, streaming assistant text and tool activity as it happens. The stream ends with
    /// exactly one terminal <see cref="AIOrchestrationUpdateKind.Completed"/> update carrying the
    /// settled <see cref="AIOrchestrationResult"/>. This is the primitive; <see cref="RunAsync"/>
    /// drains it.
    /// </summary>
    /// <param name="request">The conversation and the tools to expose.</param>
    /// <param name="cancellationToken">Cancellation token (propagate the request-abort token).</param>
    IAsyncEnumerable<AIOrchestrationUpdate> RunStreamingAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Runs the loop to completion for a single request (buffered drain of <see cref="RunStreamingAsync"/>).</summary>
    /// <param name="request">The conversation and the tools to expose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final answer plus the transcript, audit trail, and usage totals.</returns>
    Task<AIOrchestrationResult> RunAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken = default);
}
