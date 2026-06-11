using System.ComponentModel.DataAnnotations;

namespace Granit.Privacy.AI.Options;

/// <summary>
/// Configuration options for AI-powered PII detection.
/// </summary>
/// <remarks>
/// Bound to the <c>Privacy:AI</c> configuration section.
/// <para>
/// <b>Data sovereignty warning:</b> PII detection sends text to an AI model.
/// Configure <see cref="WorkspaceName"/> to point to a workspace using a local model
/// (e.g. Ollama) or a provider with a Data Processing Agreement (e.g. Azure OpenAI with DPA)
/// to ensure personal data does not leave your security perimeter.
/// </para>
/// </remarks>
public sealed class PrivacyAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Privacy:AI";

    /// <summary>
    /// AI workspace name to use for PII detection. Defaults to <c>"default"</c>.
    /// </summary>
    /// <remarks>
    /// Recommended: configure a dedicated workspace pointing to Ollama (local inference)
    /// or Azure OpenAI with a DPA to keep PII within the security perimeter.
    /// </remarks>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Maximum time in seconds to wait for PII detection to complete. Defaults to <c>15</c>.
    /// </summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Behavior when PII detection fails (LLM timeout, deserialization error, provider outage).
    /// <see cref="PiiDetectionFailMode.Closed"/> assumes PII is present (conservative — recommended for production).
    /// <see cref="PiiDetectionFailMode.Open"/> assumes no PII (permissive — for development/testing).
    /// Defaults to <see cref="PiiDetectionFailMode.Closed"/>.
    /// </summary>
    public PiiDetectionFailMode FailMode { get; set; } = PiiDetectionFailMode.Closed;
}

/// <summary>
/// Defines the behavior when PII detection fails.
/// </summary>
public enum PiiDetectionFailMode
{
    /// <summary>Assume PII is present on failure (conservative — safe default).</summary>
    Closed = 0,

    /// <summary>Assume no PII on failure (permissive — for development/testing only).</summary>
    Open = 1,
}
