namespace Granit.Authorization.AI.Options;

/// <summary>
/// Configuration options for AI-powered access anomaly detection.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Authorization</c> configuration section.
/// </remarks>
public sealed class AuthorizationAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Authorization";

    /// <summary>
    /// Name of the AI workspace used for access anomaly detection.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM anomaly detection call.
    /// Authorization checks should not block requests, so keep this low.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
