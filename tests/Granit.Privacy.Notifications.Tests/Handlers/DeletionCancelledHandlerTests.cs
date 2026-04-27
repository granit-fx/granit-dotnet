using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionCancelledHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesCancellationConfirmation_ToUser()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;
        DeletionCancelledEto evt = new(requestId, userId, cancelledAt);

        await DeletionCancelledHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionCancelledNotificationType.Instance,
            Arg.Is<PrivacyDeletionCancelledNotificationData>(d =>
                d.RequestId == requestId && d.CancelledAt == cancelledAt),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
