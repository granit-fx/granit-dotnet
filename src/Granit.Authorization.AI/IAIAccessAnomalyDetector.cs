namespace Granit.Authorization.AI;

/// <summary>
/// Evaluates access patterns for suspicious behavior using AI-powered anomaly detection.
/// </summary>
/// <remarks>
/// <para>
/// This is a reusable service that authorization components can call to assess risk.
/// It is NOT an authorization handler itself — handlers use this service to enrich
/// their decisions with AI-powered anomaly analysis.
/// </para>
/// <para>
/// The implementation uses a fail-open design: when the LLM is unavailable or
/// times out, access is allowed and a warning is logged for manual review.
/// </para>
/// </remarks>
public interface IAIAccessAnomalyDetector
{
    /// <summary>
    /// Evaluates the given access pattern for anomalies.
    /// </summary>
    /// <param name="userId">The identifier of the user requesting access.</param>
    /// <param name="permission">The permission being requested.</param>
    /// <param name="context">
    /// Optional context about the access request (e.g. "admin panel access at 3 AM",
    /// "bulk data export from new IP address"). Helps the LLM make more accurate assessments.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="AccessRiskScore"/> containing the risk assessment.
    /// Returns a zero-risk score when the AI service is unavailable (fail-open).
    /// </returns>
    Task<AccessRiskScore> EvaluateAccessAsync(
        string userId,
        string permission,
        string? context = null,
        CancellationToken cancellationToken = default);
}
