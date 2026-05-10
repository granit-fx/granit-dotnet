using Granit.Notifications;

namespace Granit.Documents.Notifications;

/// <summary>
/// Notification raised when a tenant's <c>TenantStorageQuota.UsageBytes</c> first
/// crosses an early-warning threshold (F10.4 — defaults to 80% of the limit). The
/// host application is responsible for publishing this notification when its quota
/// observability layer detects the threshold crossing; the framework ships only the
/// notification type + template.
/// </summary>
public sealed class QuotaWarningNotificationType
    : NotificationType<QuotaWarningNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly QuotaWarningNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "documents.quota_warning";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.Email];

    /// <inheritdoc/>
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;
}

/// <summary>Data payload for the <c>documents.quota_warning</c> notification.</summary>
public sealed record QuotaWarningNotificationData(
    Guid TenantId,
    long UsageBytes,
    long LimitBytes,
    int UsagePercent);
