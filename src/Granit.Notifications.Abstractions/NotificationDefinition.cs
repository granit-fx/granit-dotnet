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

    /// <summary>
    /// Permission name (<c>Group.Resource.Action</c>) the recipient must hold for
    /// this notification to surface in the preferences UI. When <c>null</c>
    /// (default), no permission gate is applied — the notification is visible to
    /// every authenticated user. When set, the <c>GET /notifications/types</c>
    /// endpoint filters out definitions whose permission the current user does
    /// not have (resolved via <c>IPermissionChecker</c> when available).
    /// </summary>
    /// <remarks>
    /// This is a UI-only filter; it does not gate fan-out. Fan-out remains
    /// driven by <see cref="DefaultChannels"/> and <see cref="AllowUserOptOut"/>
    /// — a notification dispatched to a recipient is delivered even if the
    /// recipient lacks the permission listed here.
    /// </remarks>
    public string? RequiredPermission { get; init; }

    /// <summary>
    /// Feature name (<c>Granit.Features</c>) that must be enabled on the current
    /// tenant for this notification to surface in the preferences UI. When
    /// <c>null</c> (default), no feature gate is applied. When set, the
    /// <c>GET /notifications/types</c> endpoint filters via
    /// <see cref="Abstractions.INotificationFeatureGate"/> if the host registered one.
    /// </summary>
    /// <remarks>
    /// UI-only filter; does not gate fan-out (same rationale as
    /// <see cref="RequiredPermission"/>).
    /// </remarks>
    public string? RequiredFeature { get; init; }

    public NotificationDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
