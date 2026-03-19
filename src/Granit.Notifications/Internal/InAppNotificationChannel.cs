using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Timing;

namespace Granit.Notifications.Internal;

/// <summary>
/// Built-in InApp channel: persists notifications in the user's inbox
/// via <see cref="IUserNotificationWriter"/>. The database record is the source of truth.
/// </summary>
internal sealed class InAppNotificationChannel(
    IUserNotificationWriter userNotificationWriter,
    IGuidGenerator guidGenerator,
    IClock clock) : INotificationChannel
{
    public string Name => NotificationChannels.InApp;

    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        var notification = UserNotification.Create(
            guidGenerator.Create(),
            context.NotificationId,
            context.NotificationTypeName,
            context.Severity,
            context.RecipientUserId,
            context.Data,
            clock.Now,
            context.TenantId,
            context.RelatedEntity?.EntityType,
            context.RelatedEntity?.EntityId);

        await userNotificationWriter.InsertAsync(notification, cancellationToken).ConfigureAwait(false);
    }
}
