using Granit.Notifications;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Notification type for a promotional credit approaching its expiration date — sent
/// proactively to the party (end user) who owns the balance so they can use the credit
/// before any value is silently lost.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — the user has a
/// limited time window to act before value disappears from their account.
/// </remarks>
public sealed class CreditExpiringNotificationType
    : NotificationType<CreditExpiringNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly CreditExpiringNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "customer-balance.credit_expiring";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a "credit expiring soon" notification.
/// </summary>
/// <param name="BalanceAccountId">Balance account holding the credit.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owns the balance.</param>
/// <param name="CreditId">Identifier of the promotional credit transaction.</param>
/// <param name="Amount">Original credited amount, positive.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
/// <param name="ExpiresAt">Scheduled expiration timestamp of the credit.</param>
/// <param name="DaysUntilExpiry">
/// Whole-day countdown until <paramref name="ExpiresAt"/>, computed by the handler at
/// dispatch time. Embedded in the data so templates render a friendly "Your credit
/// expires in N days" header without doing date arithmetic in Scriban.
/// </param>
public sealed record CreditExpiringNotificationData(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    Guid CreditId,
    decimal Amount,
    string Currency,
    DateTimeOffset ExpiresAt,
    int DaysUntilExpiry);
