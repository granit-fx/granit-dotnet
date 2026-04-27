using Granit.CustomerBalance.Events;
using Granit.Notifications.Abstractions;

namespace Granit.CustomerBalance.Notifications.Handlers;

/// <summary>
/// Handles <see cref="BalanceCreditedEto"/> by sending a
/// <see cref="CreditGrantedNotificationType"/> to the party (end user) who owns the
/// balance, so they discover the credit before their next charge.
/// </summary>
/// <remarks>
/// Recipient strategy: directly addressed to the owning <c>PartyId</c> — credit is a
/// per-user signal, not a tenant-level one.
/// </remarks>
public class CreditGrantedNotificationHandler
{
    public static async Task HandleAsync(
        BalanceCreditedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            CreditGrantedNotificationType.Instance,
            new CreditGrantedNotificationData(
                evt.BalanceAccountId,
                evt.TenantId,
                evt.PartyId,
                evt.Amount,
                evt.Currency,
                evt.Source.ToString()),
            [evt.PartyId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
