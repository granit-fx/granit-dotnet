using Granit.CustomerBalance.Events;
using Granit.Notifications.Abstractions;

namespace Granit.CustomerBalance.Notifications.Handlers;

/// <summary>
/// Handles <see cref="CreditExpiredEto"/> by fanning out a
/// <see cref="CreditExpiredNotificationType"/> to every tenant administrator subscribed
/// to that notification type.
/// </summary>
/// <remarks>
/// <para>
/// Recipient strategy: <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>.
/// Unlike <c>BalanceCreditedEto</c>, the <c>CreditExpiredEto</c> payload does not carry
/// a <c>PartyId</c> (only <c>BalanceAccountId</c>), so we cannot directly address the
/// owning end user. Admins opt in through the notifications admin UI and are
/// tenant-scoped by the subscription engine — they can then follow up with the affected
/// user or adjust the expiration policy.
/// </para>
/// <para>
/// If <c>CreditExpiredEto</c> later grows a <c>PartyId</c> field, this handler should
/// switch to <c>INotificationPublisher.PublishAsync</c> with
/// <c>[evt.PartyId.ToString()]</c> as recipients, mirroring
/// <see cref="CreditGrantedNotificationHandler"/>.
/// </para>
/// </remarks>
public class CreditExpiredNotificationHandler
{
    public static async Task HandleAsync(
        CreditExpiredEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            CreditExpiredNotificationType.Instance,
            new CreditExpiredNotificationData(
                evt.BalanceAccountId,
                evt.TenantId,
                evt.Amount,
                evt.Currency),
            cancellationToken).ConfigureAwait(false);
    }
}
