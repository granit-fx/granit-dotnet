using System.ComponentModel.DataAnnotations;

namespace Granit.Observability.AI.Options;

/// <summary>
/// Configuration options for AI-powered observability features.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Observability</c> configuration section.
/// </remarks>
public sealed class ObservabilityAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Observability";

    /// <summary>
    /// Name of the AI workspace to use for log analysis.
    /// When <c>null</c>, the default workspace from <c>GranitAIOptions</c> is used.
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Timeout in seconds for AI analysis requests.
    /// </summary>
    [Range(1, 3600)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of log entries to include in a single analysis request.
    /// Entries beyond this limit are truncated (most recent entries are kept).
    /// </summary>
    [Range(1, 10_000)]
    public int MaxLogEntries { get; set; } = 500;
}
