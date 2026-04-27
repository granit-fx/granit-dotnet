using Granit.CustomerBalance.Events;
using Granit.Notifications.Abstractions;

namespace Granit.CustomerBalance.Notifications.Handlers;

/// <summary>
/// Handles <see cref="CreditExpiredEto"/> by sending a
/// <see cref="CreditExpiredNotificationType"/> directly to the party (end user) who
/// owned the balance, so they are informed that value was lost from their account.
/// </summary>
/// <remarks>
/// Recipient strategy: directly addressed to the owning <c>PartyId</c> — credit
/// expiration is a per-user signal, mirroring <see cref="CreditGrantedNotificationHandler"/>.
/// </remarks>
public class CreditExpiredNotificationHandler
{
    public static async Task HandleAsync(
        CreditExpiredEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            CreditExpiredNotificationType.Instance,
            new CreditExpiredNotificationData(
                evt.BalanceAccountId,
                evt.TenantId,
                evt.PartyId,
                evt.Amount,
                evt.Currency),
            [evt.PartyId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
