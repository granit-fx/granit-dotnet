using Granit.Notifications;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Notification type for a credit added to a balance account — sent to the party
/// (end user) who owns the balance so they know value is available before their next
/// charge.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — purely
/// informational; no action required from the recipient.
/// </remarks>
public sealed class CreditGrantedNotificationType
    : NotificationType<CreditGrantedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly CreditGrantedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "customer-balance.credit_granted";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a credit-granted notification.
/// </summary>
/// <param name="BalanceAccountId">Balance account that received the credit.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owns the balance.</param>
/// <param name="Amount">Credited amount, positive.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
/// <param name="Source">
/// Origin of the credit (e.g. <c>Promotion</c>, <c>Overpayment</c>, <c>ManualAdjustment</c>) —
/// rendered in the template so the user understands where the credit came from.
/// </param>
public sealed record CreditGrantedNotificationData(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    decimal Amount,
    string Currency,
    string Source);
