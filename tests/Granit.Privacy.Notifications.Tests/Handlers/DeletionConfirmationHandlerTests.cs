using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionConfirmationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesConfirmationNotification_ToUser()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset executedAt = DateTimeOffset.UtcNow;
        DeletionExecutedEto evt = new(requestId, userId, executedAt);

        await DeletionConfirmationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionConfirmationNotificationType.Instance,
            Arg.Is<PrivacyDeletionConfirmationNotificationData>(d =>
                d.RequestId == requestId && d.ExecutedAt == executedAt),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
