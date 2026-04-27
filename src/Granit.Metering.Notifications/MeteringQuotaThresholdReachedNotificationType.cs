using Granit.Notifications;

namespace Granit.Metering.Notifications;

/// <summary>
/// Notification type for a metered usage that has crossed the early-warning threshold
/// (default 80% of the configured limit) — sent to tenant administrators so they have
/// time to upgrade their plan or rebalance usage before throttling kicks in.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — usage is
/// elevated but not yet at the hard limit, so action is recommended but not urgent.
/// </remarks>
public sealed class MeteringQuotaThresholdReachedNotificationType
    : NotificationType<MeteringQuotaThresholdReachedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly MeteringQuotaThresholdReachedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "metering.quota_threshold_reached";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a quota-threshold-reached notification.
/// </summary>
/// <param name="TenantId">Tenant whose usage crossed the warning threshold.</param>
/// <param name="MeterDefinitionId">Identifier of the meter definition.</param>
/// <param name="MeterName">Human-readable meter name (e.g. <c>api_calls</c>).</param>
/// <param name="CurrentUsage">Usage accumulated in the current billing period.</param>
/// <param name="Limit">Configured quota limit for the period.</param>
/// <param name="PercentUsed">
/// Percent of the limit consumed at the moment of the alert (0-100).
/// Echoes the value carried by <c>QuotaThresholdReachedEto.PercentUsed</c>.
/// </param>
public sealed record MeteringQuotaThresholdReachedNotificationData(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    decimal CurrentUsage,
    decimal Limit,
    decimal PercentUsed);
