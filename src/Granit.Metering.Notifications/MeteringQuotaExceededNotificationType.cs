using Granit.Notifications;

namespace Granit.Metering.Notifications;

/// <summary>
/// Notification type for a metered usage that has reached or exceeded 100% of the
/// configured quota — sent to tenant administrators so they can upgrade or rebalance
/// before requests start being throttled or rejected.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Error"/> — the tenant is
/// at or past the hard limit and downstream enforcement (throttling, hard stop) is
/// imminent or already in effect.
/// </remarks>
public sealed class MeteringQuotaExceededNotificationType
    : NotificationType<MeteringQuotaExceededNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly MeteringQuotaExceededNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "metering.quota_exceeded";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Error;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a quota-exceeded notification.
/// </summary>
/// <param name="TenantId">Tenant whose usage reached or exceeded the configured quota.</param>
/// <param name="MeterDefinitionId">Identifier of the meter definition.</param>
/// <param name="MeterName">Human-readable meter name (e.g. <c>api_calls</c>).</param>
/// <param name="CurrentUsage">Usage accumulated in the current billing period.</param>
/// <param name="Limit">Configured quota limit for the period.</param>
/// <param name="PercentUsed">
/// Percent of the limit consumed at the moment of the alert. Computed as
/// <c>CurrentUsage / Limit * 100</c> and clamped to a non-negative value; values above
/// 100 indicate overage.
/// </param>
public sealed record MeteringQuotaExceededNotificationData(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    decimal CurrentUsage,
    decimal Limit,
    decimal PercentUsed);
