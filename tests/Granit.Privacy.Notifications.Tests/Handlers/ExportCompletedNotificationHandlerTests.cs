using Granit.Notifications.Abstractions;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class ExportCompletedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_CompleteExport_PublishesReadyNotification()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        ExportCompletedEto evt = new(
            RequestId: requestId,
            UserId: userId,
            ArchiveBlobReferenceId: "personal-data-export/abc",
            IsPartial: false,
            MissingProviders: [],
            Fragments: [],
            Regulation: "EU_GDPR",
            RequestedAt: requestedAt);

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            Arg.Is<PrivacyExportReadyNotificationData>(d =>
                d.RequestId == requestId &&
                d.ArchiveBlobReferenceId == "personal-data-export/abc" &&
                d.RequestedAt == requestedAt &&
                d.Regulation == "EU_GDPR"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());

        await publisher.DidNotReceive().PublishAsync(
            PrivacyExportFailedNotificationType.Instance,
            Arg.Any<PrivacyExportFailedNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PartialExport_PublishesFailedNotificationWithMissingProviders()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        IReadOnlyList<string> missing = ["PatientModule", "BlobStorageModule"];
        ExportCompletedEto evt = new(
            RequestId: requestId,
            UserId: userId,
            ArchiveBlobReferenceId: "personal-data-export/abc",
            IsPartial: true,
            MissingProviders: missing,
            Fragments: [],
            Regulation: "EU_GDPR",
            RequestedAt: requestedAt);

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyExportFailedNotificationType.Instance,
            Arg.Is<PrivacyExportFailedNotificationData>(d =>
                d.RequestId == requestId &&
                d.ArchiveBlobReferenceId == "personal-data-export/abc" &&
                d.MissingProviders.SequenceEqual(missing) &&
                d.MissingProvidersDisplay == "PatientModule, BlobStorageModule" &&
                d.RequestedAt == requestedAt &&
                d.Regulation == "EU_GDPR"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());

        await publisher.DidNotReceive().PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            Arg.Any<PrivacyExportReadyNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }
}
