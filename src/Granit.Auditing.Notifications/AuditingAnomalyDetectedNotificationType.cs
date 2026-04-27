using Granit.Auditing.Domain;
using Granit.Notifications;

namespace Granit.Auditing.Notifications;

/// <summary>
/// Notification type raised when an audit entry whose
/// <see cref="AuditCategory"/> is in the configured "alertable" set is persisted.
/// Pages platform administrators / SOC so they can react to a possible security
/// incident in time, rather than discovering it during a quarterly log review.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> is the
/// sensible default — an authorization failure or a privileged configuration
/// change warrants attention but is not, on its own, evidence of a confirmed
/// breach. Recipients are resolved via the notifications subscription system
/// (admins / on-call opt in through the admin UI), which keeps this bridge free
/// of any tenant-admin or SOC-roster lookup service.
/// </remarks>
public sealed class AuditingAnomalyDetectedNotificationType
    : NotificationType<AuditingAnomalyDetectedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly AuditingAnomalyDetectedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "auditing.anomaly_detected";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the auditing anomaly notification.
/// </summary>
/// <param name="AuditEntryId">Identifier of the persisted audit entry — used to
/// build the deep link into the audit-log admin page so the responder can pivot
/// straight to the full record (entity changes, property diffs, request context)
/// instead of starting from a search.</param>
/// <param name="Category">Audit category that triggered the alert (e.g.
/// <see cref="AuditCategory.AccessDenied"/>). Carried as its string name rather
/// than the numeric value so the email is self-describing without a legend.</param>
/// <param name="OccurredAt">When the audited operation occurred (UTC).</param>
/// <param name="ActorUserId">Subject who triggered the audited operation. May be
/// the literal <c>"anonymous"</c> for unauthenticated <see cref="AuditCategory.AccessDenied"/>
/// attempts (e.g. expired token).</param>
/// <param name="EntityChangeCount">Number of entity changes in the audited batch
/// — a crude blast-radius indicator for configuration changes.</param>
/// <param name="TenantId">Tenant identifier when the operation was tenant-scoped,
/// otherwise <see langword="null"/> for global / platform operations.</param>
public sealed record AuditingAnomalyDetectedNotificationData(
    Guid AuditEntryId,
    string Category,
    DateTimeOffset OccurredAt,
    string ActorUserId,
    int EntityChangeCount,
    Guid? TenantId);
