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

    /// <summary>
    /// When <c>true</c>, registered
    /// <see cref="Abstractions.INotificationDeliveryGate"/>s are skipped for this
    /// notification — delivery proceeds on every default channel even when the
    /// recipient is in <c>DoNotDisturb</c> or appears offline. Reserved for
    /// security-critical alerts (suspicious login, MFA disabled, breach notices).
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AllowDoNotDisturbBypass { get; init; }

    public NotificationDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
