using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionAcknowledgedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesAcknowledgement_ToUser()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        PersonalDataDeletionRequestedEto evt = new(
            RequestId: requestId,
            UserId: userId,
            RequestedBy: "self",
            RequestedAt: requestedAt,
            Reason: "user-initiated",
            Regulation: "EU_GDPR");

        await DeletionAcknowledgedHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            Arg.Is<PrivacyDeletionAcknowledgedNotificationData>(d =>
                d.RequestId == requestId &&
                d.RequestedAt == requestedAt &&
                d.Regulation == "EU_GDPR"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
