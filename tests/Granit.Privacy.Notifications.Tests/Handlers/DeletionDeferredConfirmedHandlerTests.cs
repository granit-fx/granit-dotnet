using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionDeferredConfirmedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesDeferredConfirmation_ToUser()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        DateTimeOffset scheduledAt = requestedAt.AddDays(30);
        DeletionDeferredEto evt = new(
            RequestId: requestId,
            UserId: userId,
            RequestedBy: "self",
            RequestedAt: requestedAt,
            Reason: "user-initiated",
            ScheduledDeletionAt: scheduledAt,
            Regulation: "EU_GDPR");

        await DeletionDeferredConfirmedHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionDeferredConfirmedNotificationType.Instance,
            Arg.Is<PrivacyDeletionDeferredConfirmedNotificationData>(d =>
                d.RequestId == requestId &&
                d.RequestedAt == requestedAt &&
                d.ScheduledDeletionAt == scheduledAt &&
                d.Regulation == "EU_GDPR"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
