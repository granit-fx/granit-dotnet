using Granit.Notifications;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Notification type for a credit that has expired and was deducted from the balance —
/// fanned out to tenant administrators because <c>CreditExpiredEto</c> does not carry
/// the owning party identifier (only <c>BalanceAccountId</c>).
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — value was
/// silently lost by the user, so admins should follow up or adjust expiration policy.
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
/// <param name="Amount">Amount that expired and was deducted from the balance, positive.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
public sealed record CreditExpiredNotificationData(
    Guid BalanceAccountId,
    Guid TenantId,
    decimal Amount,
    string Currency);
