namespace Granit.Authorization.AI;

/// <summary>
/// Result of an AI access anomaly evaluation, containing a risk score and supporting details.
/// </summary>
/// <param name="Score">
/// Risk score between 0.0 (no risk) and 1.0 (critical risk).
/// Values above 0.7 typically indicate suspicious access patterns.
/// </param>
/// <param name="Reasoning">Human-readable explanation of the risk assessment.</param>
/// <param name="RiskFactors">Specific risk factors identified during the evaluation.</param>
public sealed record AccessRiskScore(
    double Score,
    string Reasoning,
    IReadOnlyList<string> RiskFactors);
