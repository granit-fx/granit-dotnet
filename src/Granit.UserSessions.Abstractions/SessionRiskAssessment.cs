namespace Granit.UserSessions;

/// <summary>
/// Outcome of evaluating a session for anomalies.
/// </summary>
/// <param name="Level">Coarse risk classification.</param>
/// <param name="Score">Normalized risk score in <c>[0, 1]</c>.</param>
/// <param name="Reasons">Machine-readable reason codes that contributed (e.g. <c>"impossible_travel"</c>).</param>
/// <param name="Explanation">Optional human-readable explanation (PII-safe).</param>
public sealed record SessionRiskAssessment(
    SessionRiskLevel Level,
    double Score,
    IReadOnlyList<string> Reasons,
    string? Explanation = null)
{
    /// <summary>A no-risk assessment — the value returned when anomaly detection is disabled.</summary>
    public static SessionRiskAssessment None { get; } = new(SessionRiskLevel.None, 0d, []);
}
