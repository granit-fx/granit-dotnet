using System.ComponentModel.DataAnnotations;

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
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gates whether the notification's <c>Data</c> payload is forwarded to the LLM when
    /// generating content. The payload may contain personal data (GDPR), so forwarding is
    /// opt-in: when <see langword="false"/> (the default) only the notification type,
    /// severity and culture reach the model.
    /// </summary>
    public bool AllowPersonalDataInPrompts { get; set; }
}
