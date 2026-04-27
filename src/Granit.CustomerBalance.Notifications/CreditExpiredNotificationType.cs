using Granit.Notifications;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Notification type for a credit that has expired and was deducted from the balance —
/// sent directly to the party (end user) who owned the balance so they are informed
/// that value was lost from their account.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — value was
/// silently lost by the user, so they should be made aware (and may choose to top up
/// or contact support).
/// </remarks>
public sealed class CreditExpiredNotificationType
    : NotificationType<CreditExpiredNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly CreditExpiredNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "customer-balance.credit_expired";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a credit-expired notification.
/// </summary>
/// <param name="BalanceAccountId">Balance account whose credit expired.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owned the balance.</param>
/// <param name="Amount">Amount that expired and was deducted from the balance, positive.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
public sealed record CreditExpiredNotificationData(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    decimal Amount,
    string Currency);
