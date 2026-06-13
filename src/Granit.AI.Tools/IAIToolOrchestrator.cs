namespace Granit.AI.Tools;

/// <summary>
/// Drives the agentic loop (ADR-067): send the conversation to the model with the available
/// tool declarations, execute any tools the model requests, feed the results back, and repeat
/// until the model stops requesting tools — bounded by an iteration cap and per-result context
/// guards, and stamping usage on completion.
/// </summary>
public interface IAIToolOrchestrator
{
    /// <summary>Runs the loop to completion for a single request.</summary>
    /// <param name="request">The conversation and the tools to expose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final answer plus the transcript, audit trail, and usage totals.</returns>
    Task<AIOrchestrationResult> RunAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken = default);
}
