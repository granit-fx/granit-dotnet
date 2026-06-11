namespace Granit.Authorization.AI.Options;

/// <summary>
/// Configuration options for AI-powered access anomaly detection.
/// </summary>
/// <remarks>
/// Bound to the <c>Authorization:AI</c> configuration section.
/// </remarks>
public sealed class AuthorizationAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Authorization:AI";

    /// <summary>
    /// Name of the AI workspace used for access anomaly detection.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM anomaly detection call.
    /// Authorization checks should not block requests, so keep this low.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Risk score returned when the LLM is unavailable (timeout, error).
    /// A value of 0.5 signals "uncertain" — callers should apply their own policy.
    /// Default: <c>0.5</c>. Set to <c>0.0</c> for fail-open or <c>1.0</c> for fail-closed.
    /// </summary>
    public double UnavailableRiskScore { get; set; } = 0.5;
}
