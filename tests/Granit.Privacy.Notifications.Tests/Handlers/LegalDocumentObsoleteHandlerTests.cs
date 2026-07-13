using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Events;
using Granit.Privacy.Notifications.Handlers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class LegalDocumentObsoleteHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithSingleSmallBatch_PublishesOnceWithAllUsers()
    {
        // Below BatchSize (1000) — the stream ends before the batch ever fills, so the
        // "flush remaining" branch must be the one that fires, exactly once.
        ILegalAgreementStoreReader storeReader = Substitute.For<ILegalAgreementStoreReader>();
        ILegalDocumentRegistry documentRegistry = Substitute.For<ILegalDocumentRegistry>();
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        Guid[] userIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        storeReader.StreamUsersByDocumentVersionAsync("privacy-policy", "1.0", Arg.Any<CancellationToken>())
            .Returns(ci => AsAsync(userIds));
        documentRegistry.GetDefinitionAsync("privacy-policy", Arg.Any<CancellationToken>())
            .Returns(new LegalDocumentDefinition("privacy-policy", "2.0", "Privacy Policy"));

        LegalAgreementObsoleteEto evt = new(
            DocumentId: "privacy-policy",
            OldVersion: "1.0",
            NewVersion: "2.0");

        await LegalDocumentObsoleteHandler.HandleAsync(
            evt, storeReader, documentRegistry, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyLegalDocumentObsoleteNotificationType.Instance,
            Arg.Is<PrivacyLegalDocumentObsoleteNotificationData>(d =>
                d.DocumentId == "privacy-policy" &&
                d.DocumentDisplayName == "Privacy Policy" &&
                d.OldVersion == "1.0" &&
                d.NewVersion == "2.0"),
            Arg.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 3 &&
                userIds.All(id => ids.Contains(id.ToString()))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithExactlyBatchSizeUsers_PublishesOnceWithNoEmptyFlush()
    {
        // Exactly BatchSize (1000) users — the in-loop flush must fire once for the full
        // batch, and the trailing "flush remaining" branch must NOT fire an extra empty call.
        // The handler reuses/clears the same List<string> instance across calls, so batch
        // sizes are snapshotted inside a .Returns() callback at call-time rather than
        // asserted post-hoc on the (by-then-mutated) captured argument.
        ILegalAgreementStoreReader storeReader = Substitute.For<ILegalAgreementStoreReader>();
        ILegalDocumentRegistry documentRegistry = Substitute.For<ILegalDocumentRegistry>();
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        List<int> publishedBatchSizes = [];
#pragma warning disable CA2012 // NSubstitute setup pattern — ValueTask not consumed directly
        publisher.PublishAsync(
                Arg.Any<NotificationType<PrivacyLegalDocumentObsoleteNotificationData>>(),
                Arg.Any<PrivacyLegalDocumentObsoleteNotificationData>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                publishedBatchSizes.Add(ci.ArgAt<IReadOnlyList<string>>(2).Count);
                return ValueTask.CompletedTask;
            });
#pragma warning restore CA2012

        Guid[] userIds = [.. Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid())];
        storeReader.StreamUsersByDocumentVersionAsync("privacy-policy", "1.0", Arg.Any<CancellationToken>())
            .Returns(ci => AsAsync(userIds));
        documentRegistry.GetDefinitionAsync("privacy-policy", Arg.Any<CancellationToken>())
            .Returns(new LegalDocumentDefinition("privacy-policy", "2.0", "Privacy Policy"));

        LegalAgreementObsoleteEto evt = new(
            DocumentId: "privacy-policy",
            OldVersion: "1.0",
            NewVersion: "2.0");

        await LegalDocumentObsoleteHandler.HandleAsync(
            evt, storeReader, documentRegistry, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyLegalDocumentObsoleteNotificationType.Instance,
            Arg.Any<PrivacyLegalDocumentObsoleteNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
        publishedBatchSizes.ShouldBe([1000]);
    }

    [Fact]
    public async Task HandleAsync_WithBatchSizePlusOneUsers_PublishesTwice()
    {
        // BatchSize + 1 (1001) users — one full batch of 1000 flushed in-loop, then a
        // second call for the single remaining user. Same snapshot-via-.Returns()-callback
        // approach as above, since the handler's batch list is cleared and reused between calls.
        ILegalAgreementStoreReader storeReader = Substitute.For<ILegalAgreementStoreReader>();
        ILegalDocumentRegistry documentRegistry = Substitute.For<ILegalDocumentRegistry>();
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        List<int> publishedBatchSizes = [];
#pragma warning disable CA2012 // NSubstitute setup pattern — ValueTask not consumed directly
        publisher.PublishAsync(
                Arg.Any<NotificationType<PrivacyLegalDocumentObsoleteNotificationData>>(),
                Arg.Any<PrivacyLegalDocumentObsoleteNotificationData>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                publishedBatchSizes.Add(ci.ArgAt<IReadOnlyList<string>>(2).Count);
                return ValueTask.CompletedTask;
            });
#pragma warning restore CA2012

        Guid[] userIds = [.. Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid())];
        storeReader.StreamUsersByDocumentVersionAsync("privacy-policy", "1.0", Arg.Any<CancellationToken>())
            .Returns(ci => AsAsync(userIds));
        documentRegistry.GetDefinitionAsync("privacy-policy", Arg.Any<CancellationToken>())
            .Returns(new LegalDocumentDefinition("privacy-policy", "2.0", "Privacy Policy"));

        LegalAgreementObsoleteEto evt = new(
            DocumentId: "privacy-policy",
            OldVersion: "1.0",
            NewVersion: "2.0");

        await LegalDocumentObsoleteHandler.HandleAsync(
            evt, storeReader, documentRegistry, publisher, CancellationToken.None);

        await publisher.Received(2).PublishAsync(
            PrivacyLegalDocumentObsoleteNotificationType.Instance,
            Arg.Any<PrivacyLegalDocumentObsoleteNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
        publishedBatchSizes.ShouldBe([1000, 1]);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownDocument_FallsBackToDocumentIdAsDisplayName()
    {
        // GetDefinition returning null (deleted/misconfigured document registration) must
        // not crash the handler — display name falls back to the raw DocumentId.
        ILegalAgreementStoreReader storeReader = Substitute.For<ILegalAgreementStoreReader>();
        ILegalDocumentRegistry documentRegistry = Substitute.For<ILegalDocumentRegistry>();
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        Guid[] userIds = [Guid.NewGuid()];
        storeReader.StreamUsersByDocumentVersionAsync("unknown-doc", "1.0", Arg.Any<CancellationToken>())
            .Returns(ci => AsAsync(userIds));
        documentRegistry.GetDefinitionAsync("unknown-doc", Arg.Any<CancellationToken>()).Returns((LegalDocumentDefinition?)null);

        LegalAgreementObsoleteEto evt = new(
            DocumentId: "unknown-doc",
            OldVersion: "1.0",
            NewVersion: "2.0");

        await LegalDocumentObsoleteHandler.HandleAsync(
            evt, storeReader, documentRegistry, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyLegalDocumentObsoleteNotificationType.Instance,
            Arg.Is<PrivacyLegalDocumentObsoleteNotificationData>(d => d.DocumentDisplayName == "unknown-doc"),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<Guid> AsAsync(IEnumerable<Guid> userIds)
    {
        foreach (Guid userId in userIds)
        {
            yield return userId;
            await Task.Yield();
        }
    }
}
