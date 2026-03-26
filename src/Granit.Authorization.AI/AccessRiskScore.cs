namespace Granit.Authorization.AI;

/// <summary>
/// Result of an AI access anomaly evaluation, containing a risk score and supporting details.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsAdvisoryOnly"/> is always <c>true</c>: this score MUST NOT be used as the sole
/// factor in authorization deny decisions. AI output is non-deterministic and susceptible to
/// prompt injection — always combine with deterministic authorization checks.
/// </para>
/// </remarks>
/// <param name="Score">
/// Risk score clamped to [0.0, 1.0]. Values above 0.7 typically indicate suspicious access patterns.
/// </param>
/// <param name="Reasoning">Human-readable explanation of the risk assessment.</param>
/// <param name="RiskFactors">Specific risk factors identified during the evaluation.</param>
public sealed record AccessRiskScore(
    double Score,
    string Reasoning,
    IReadOnlyList<string> RiskFactors)
{
    /// <summary>Risk score clamped to [0.0, 1.0].</summary>
    public double Score { get; } = Math.Clamp(Score, 0.0, 1.0);

    /// <summary>
    /// Always <c>true</c>. AI-generated risk scores are advisory only and MUST NOT
    /// be used as the sole factor in authorization deny decisions (OWASP LLM09).
    /// </summary>
#pragma warning disable CA1822 // Instance property by design — signals to callers that this score is advisory
    public bool IsAdvisoryOnly => true;
#pragma warning restore CA1822
}
