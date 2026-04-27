using Granit.CustomerBalance.Events;
using Granit.Notifications.Abstractions;

namespace Granit.CustomerBalance.Notifications.Handlers;

/// <summary>
/// Handles <see cref="BalanceDepletedEto"/> by sending a
/// <see cref="BalanceDepletedNotificationType"/> directly to the party (end user) who
/// owns the balance so they know there is no more credit available before their next
/// charge.
/// </summary>
/// <remarks>
/// Recipient strategy: directly addressed to the owning <c>PartyId</c> — depletion is a
/// per-user signal, mirroring <see cref="CreditGrantedNotificationHandler"/>.
/// </remarks>
public class BalanceDepletedNotificationHandler
{
    public static async Task HandleAsync(
        BalanceDepletedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            BalanceDepletedNotificationType.Instance,
            new BalanceDepletedNotificationData(
                evt.BalanceAccountId,
                evt.TenantId,
                evt.PartyId,
                evt.Currency,
                evt.DepletedAt),
            [evt.PartyId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
