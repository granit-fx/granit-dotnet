namespace Granit.Identity;

/// <summary>
/// Outcome of evaluating a session for anomalies.
/// </summary>
/// <param name="Level">Coarse risk classification.</param>
/// <param name="Score">Normalized risk score in <c>[0, 1]</c>.</param>
/// <param name="Reasons">Machine-readable reason codes that contributed (e.g. <c>"impossible_travel"</c>).</param>
/// <param name="Explanation">
/// Optional human-readable explanation. When produced by the AI layer this is <strong>untrusted model output</strong>:
/// the model is instructed to keep it PII-safe but that is not enforced. Treat it as such — sanitize and bound it
/// before rendering to a user or writing it to logs/traces, and never persist it as-is.
/// </param>
public sealed record UserSessionRiskAssessment(
    UserSessionRiskLevel Level,
    double Score,
    IReadOnlyList<string> Reasons,
    string? Explanation = null)
{
    /// <summary>A no-risk assessment — the value returned when anomaly detection is disabled.</summary>
    public static UserSessionRiskAssessment None { get; } = new(UserSessionRiskLevel.None, 0d, []);
}
