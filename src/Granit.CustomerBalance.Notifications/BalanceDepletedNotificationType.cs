using Granit.Notifications;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Notification type for a balance account that has just been depleted to zero —
/// sent to the party (end user) who owns the balance so they know there is no more
/// credit available before their next charge.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — purely
/// informational; the user may choose to top up but no action is strictly required.
/// </remarks>
public sealed class BalanceDepletedNotificationType
    : NotificationType<BalanceDepletedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly BalanceDepletedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "customer-balance.depleted";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a balance-depleted notification.
/// </summary>
/// <param name="BalanceAccountId">Balance account that has just been depleted.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owns the balance.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
/// <param name="DepletedAt">Timestamp of the transaction that depleted the balance.</param>
public sealed record BalanceDepletedNotificationData(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    string Currency,
    DateTimeOffset DepletedAt);
