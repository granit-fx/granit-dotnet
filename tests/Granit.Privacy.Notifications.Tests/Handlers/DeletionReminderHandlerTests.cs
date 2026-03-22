using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionReminderHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesReminderNotification_ToUser()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddDays(3);
        DeletionReminderDueEto evt = new(requestId, userId, deadline);

        await DeletionReminderHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionReminderNotificationType.Instance,
            Arg.Is<PrivacyDeletionReminderNotificationData>(d =>
                d.RequestId == requestId && d.ScheduledDeletionAt == deadline),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
