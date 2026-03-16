namespace Granit.Privacy.AI.Options;

/// <summary>
/// Configuration options for AI-powered PII detection.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Privacy</c> configuration section.
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
    public const string SectionName = "AI:Privacy";

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
    public int TimeoutSeconds { get; set; } = 15;
}
