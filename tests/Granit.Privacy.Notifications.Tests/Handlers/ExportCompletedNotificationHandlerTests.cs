using Granit.Notifications.Abstractions;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class ExportCompletedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_CompleteAssembly_PublishesReadyNotificationWithShardCount()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        ExportArchiveAssembledEto evt = new(
            RequestId: requestId,
            UserId: userId,
            ManifestBlobReferenceId: "abc-manifest",
            ShardCount: 3,
            IsPartial: false,
            Regulation: "EU_GDPR",
            RequestedAt: requestedAt);

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            Arg.Is<PrivacyExportReadyNotificationData>(d =>
                d.RequestId == requestId &&
                d.ShardCount == 3 &&
                d.RequestedAt == requestedAt &&
                d.Regulation == "EU_GDPR"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AssembledButFlaggedPartial_SkipsReadyNotification()
    {
        // The saga's ExportCompletedEto (handled below) is the only place that emits
        // the failure email — the assembled-event handler must not double-fire.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        ExportArchiveAssembledEto evt = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ManifestBlobReferenceId: "abc",
            ShardCount: 1,
            IsPartial: true,
            Regulation: "EU_GDPR",
            RequestedAt: DateTimeOffset.UtcNow);

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.DidNotReceive().PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            Arg.Any<PrivacyExportReadyNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CompleteSaga_SkipsFailureNotification()
    {
        // The success path used to ride ExportCompletedEto; it now defers to
        // ExportArchiveAssembledEto so the email links point at committed shards.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        ExportCompletedEto evt = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ArchiveBlobReferenceId: "personal-data-export/abc",
            IsPartial: false,
            MissingProviders: [],
            Fragments: [],
            Regulation: "EU_GDPR",
            RequestedAt: DateTimeOffset.UtcNow);

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.DidNotReceive().PublishAsync(
            PrivacyExportReadyNotificationType.Instance,
            Arg.Any<PrivacyExportReadyNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().PublishAsync(
            PrivacyExportFailedNotificationType.Instance,
            Arg.Any<PrivacyExportFailedNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PartialSaga_PublishesFailureNotification()
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

        await ExportCompletedNotificationHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

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
    }
}
