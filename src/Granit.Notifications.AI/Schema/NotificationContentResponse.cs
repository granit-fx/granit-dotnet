namespace Granit.Notifications.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI notification content generator, pinned via
/// <c>ChatResponseFormat.ForJsonSchema&lt;NotificationContentResponse&gt;()</c> (ADR-064). The
/// generator returns <c>null</c> when either field comes back blank.
/// </summary>
public sealed class NotificationContentResponse
{
    /// <summary>Concise subject line for the notification.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Body text of the notification.</summary>
    public string Body { get; set; } = string.Empty;
}
