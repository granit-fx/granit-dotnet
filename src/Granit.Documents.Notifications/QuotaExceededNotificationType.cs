using Granit.Notifications;

namespace Granit.Documents.Notifications;

/// <summary>
/// Notification raised when a tenant exhausts its storage quota and an upload is
/// rejected (F10.5). Driven by the <c>granit.documents.quota.rejected.count</c>
/// metric — when the host's quota-observability layer flips a tenant from "below
/// limit" to "at limit", it publishes this notification.
/// </summary>
public sealed class QuotaExceededNotificationType
    : NotificationType<QuotaExceededNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly QuotaExceededNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "documents.quota_exceeded";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.Email];

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Error;
}

/// <summary>Data payload for the <c>documents.quota_exceeded</c> notification.</summary>
public sealed record QuotaExceededNotificationData(
    Guid TenantId,
    long UsageBytes,
    long LimitBytes,
    long AttemptedBytes);
