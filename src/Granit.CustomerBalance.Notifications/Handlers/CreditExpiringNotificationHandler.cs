using Granit.CustomerBalance.Events;
using Granit.Notifications.Abstractions;
using Granit.Timing;

namespace Granit.CustomerBalance.Notifications.Handlers;

/// <summary>
/// Handles <see cref="CreditExpiringEto"/> by sending a
/// <see cref="CreditExpiringNotificationType"/> directly to the party (end user) who
/// owns the balance so they can use the credit before any value is silently lost.
/// </summary>
/// <remarks>
/// Recipient strategy: directly addressed to the owning <c>PartyId</c> — credit
/// expiration is a per-user signal, mirroring <see cref="CreditExpiredNotificationHandler"/>.
/// The whole-day countdown <c>DaysUntilExpiry</c> is computed here using
/// <see cref="IClock"/> rather than in the template so Scriban does not have to do date
/// arithmetic.
/// </remarks>
public class CreditExpiringNotificationHandler
{
    public static async Task HandleAsync(
        CreditExpiringEto evt,
        INotificationPublisher publisher,
        IClock clock,
        CancellationToken cancellationToken)
    {
        int daysUntilExpiry = Math.Max(0, (int)Math.Ceiling((evt.ExpiresAt - clock.Now).TotalDays));

        await publisher.PublishAsync(
            CreditExpiringNotificationType.Instance,
            new CreditExpiringNotificationData(
                evt.BalanceAccountId,
                evt.TenantId,
                evt.PartyId,
                evt.CreditId,
                evt.Amount,
                evt.Currency,
                evt.ExpiresAt,
                daysUntilExpiry),
            [evt.PartyId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
