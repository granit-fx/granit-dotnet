namespace Granit.Notifications;

/// <summary>
/// Metadata for a notification type, registered at startup via <see cref="Abstractions.INotificationDefinitionProvider"/>.
/// </summary>
public sealed class NotificationDefinition
{
    /// <summary>Unique notification type name.</summary>
    public string Name { get; }

    /// <summary>Default severity.</summary>
    public NotificationSeverity DefaultSeverity { get; init; } = NotificationSeverity.Info;

    /// <summary>Default delivery channels.</summary>
    public IReadOnlyList<string> DefaultChannels { get; init; } = [];

    /// <summary>Display name for UI.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Description for UI.</summary>
    public string? Description { get; init; }

    /// <summary>Group name for UI categorization.</summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// When <c>false</c>, the notification is always sent regardless of user preferences
    /// (e.g., security alerts, GDPR breach notifications).
    /// </summary>
    public bool AllowUserOptOut { get; init; } = true;

    public NotificationDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
