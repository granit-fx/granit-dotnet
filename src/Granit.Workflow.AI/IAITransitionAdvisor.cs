namespace Granit.Workflow.AI;

/// <summary>
/// Recommends the next workflow transition based on entity context using AI.
/// </summary>
/// <remarks>
/// <para>
/// Uses string-based state (not generic <c>TState</c>) to avoid generic complexity in the AI layer.
/// The caller is responsible for mapping between the enum state and its string representation.
/// </para>
/// <para>
/// The advisor never executes transitions — it only recommends. The human (or a threshold-based
/// policy) decides whether to proceed.
/// </para>
/// </remarks>
public interface IAITransitionAdvisor
{
    /// <summary>
    /// Recommends the best next transition based on entity context and allowed transitions.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g. "Invoice", "Document").</param>
    /// <param name="currentState">The current workflow state as a string.</param>
    /// <param name="entityContext">
    /// Contextual information about the entity (e.g. JSON representation, summary).
    /// </param>
    /// <param name="allowedTransitions">
    /// The list of allowed transition names from the current state.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A recommendation with the suggested transition, reasoning, and confidence score;
    /// or <c>null</c> if the AI cannot make a recommendation.
    /// </returns>
    Task<TransitionRecommendation?> RecommendAsync(
        string entityType,
        string currentState,
        string entityContext,
        IReadOnlyList<string> allowedTransitions,
        CancellationToken cancellationToken = default);
}
