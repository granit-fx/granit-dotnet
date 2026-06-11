namespace Granit.Notifications.AI.Options;

/// <summary>
/// Configuration options for the AI notification content generator and channel selector.
/// </summary>
/// <remarks>
/// Bound to the <c>Notifications:AI</c> configuration section.
/// </remarks>
public sealed class NotificationsAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Notifications:AI";

    /// <summary>
    /// Name of the AI workspace to use for notification content generation and channel selection.
    /// When <c>null</c>, the default workspace is used.
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Timeout in seconds for LLM requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
