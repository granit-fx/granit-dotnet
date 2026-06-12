namespace Granit.UserSessions.AnomalyDetection.Internal;

/// <summary>Structured output shape requested from the AI model.</summary>
internal sealed class UserSessionRiskResponse
{
    /// <summary>Risk level name (<c>None</c>, <c>Low</c>, <c>Medium</c>, <c>High</c>).</summary>
    public string? Level { get; set; }

    /// <summary>Risk score in <c>[0, 1]</c>.</summary>
    public double Score { get; set; }

    /// <summary>Machine-readable reason codes.</summary>
    public string[]? Reasons { get; set; }

    /// <summary>PII-safe human-readable explanation.</summary>
    public string? Explanation { get; set; }
}
