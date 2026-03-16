namespace Granit.Workflow.AI;

/// <summary>
/// Result of an AI-powered transition recommendation.
/// </summary>
/// <param name="RecommendedTransition">
/// The name of the recommended transition (matches one of the allowed transitions).
/// </param>
/// <param name="Reasoning">
/// Human-readable explanation of why this transition was recommended.
/// </param>
/// <param name="Confidence">
/// Confidence score between 0.0 and 1.0, where 1.0 indicates highest confidence.
/// </param>
public sealed record TransitionRecommendation(
    string RecommendedTransition,
    string Reasoning,
    double Confidence);
