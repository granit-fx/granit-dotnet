using System;
using System.Linq;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Events;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Tests;

public sealed class DocumentRenditionTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePending_should_initialise_status_and_timestamps()
    {
        var r = DocumentRendition.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            documentId: Guid.NewGuid(),
            documentVersionId: Guid.NewGuid(),
            type: RenditionType.Thumbnail,
            format: "image/webp",
            now: Now);

        r.Status.ShouldBe(RenditionStatus.Pending);
        r.CreatedAt.ShouldBe(Now);
        r.CompletedAt.ShouldBeNull();
        r.BlobDescriptorId.ShouldBeNull();
        r.SizeBytes.ShouldBeNull();
    }

    [Fact]
    public void CreatePending_should_reject_non_mime_format()
    {
        Should.Throw<ArgumentException>(() => DocumentRendition.Create(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            RenditionType.Thumbnail, format: "webp", now: Now));
    }

    [Fact]
    public void MarkReady_should_capture_outputs_and_emit_event()
    {
        DocumentRendition r = NewPending();
        r.MarkGenerating();

        var blob = Guid.NewGuid();
        r.MarkReady(blob, sizeBytes: 8_192, width: 200, height: 200, now: Now.AddSeconds(1));

        r.Status.ShouldBe(RenditionStatus.Ready);
        r.BlobDescriptorId.ShouldBe(blob);
        r.SizeBytes.ShouldBe(8_192);
        r.Width.ShouldBe(200);
        r.Height.ShouldBe(200);
        r.CompletedAt.ShouldBe(Now.AddSeconds(1));
        r.FailureReason.ShouldBeNull();

        r.DomainEvents.OfType<RenditionGeneratedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MarkReady_should_reject_when_not_generating()
    {
        DocumentRendition r = NewPending();
        Should.Throw<InvalidOperationException>(() =>
            r.MarkReady(Guid.NewGuid(), 1, 100, 100, Now));
    }

    [Fact]
    public void MarkFailed_should_emit_event_and_persist_reason()
    {
        DocumentRendition r = NewPending();
        r.MarkGenerating();

        r.MarkFailed("provider exhausted", Now.AddSeconds(2));

        r.Status.ShouldBe(RenditionStatus.Failed);
        r.FailureReason.ShouldBe("provider exhausted");
        r.CompletedAt.ShouldBe(Now.AddSeconds(2));
        r.DomainEvents.OfType<RenditionFailedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MarkGenerating_after_Failed_should_clear_reason_and_allow_retry()
    {
        DocumentRendition r = NewPending();
        r.MarkGenerating();
        r.MarkFailed("transient", Now);

        r.MarkGenerating();

        r.Status.ShouldBe(RenditionStatus.Generating);
        r.FailureReason.ShouldBeNull();
    }

    private static DocumentRendition NewPending() => DocumentRendition.Create(
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
        RenditionType.Thumbnail, "image/png", Now);
}
