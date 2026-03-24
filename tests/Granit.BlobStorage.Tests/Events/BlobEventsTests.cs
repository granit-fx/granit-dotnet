using Granit.BlobStorage.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Events;

public sealed class BlobEventsTests
{
    // ── BlobUploadStartedEvent ────────────────────────────────────────────────

    [Fact]
    public void BlobUploadStartedEvent_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();

        BlobUploadStartedEvent evt = new(blobId, "medical-images", "scan.pdf");

        evt.BlobId.ShouldBe(blobId);
        evt.ContainerName.ShouldBe("medical-images");
        evt.OriginalFileName.ShouldBe("scan.pdf");
    }

    [Fact]
    public void BlobUploadStartedEvent_ImplementsIDomainEvent()
    {
        BlobUploadStartedEvent evt = new(Guid.NewGuid(), "c", "f");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobValidatedEvent ───────────────────────────────────────────────────

    [Fact]
    public void BlobValidatedEvent_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();

        BlobValidatedEvent evt = new(blobId, "docs", "application/pdf", 4_500_000L);

        evt.BlobId.ShouldBe(blobId);
        evt.ContainerName.ShouldBe("docs");
        evt.VerifiedContentType.ShouldBe("application/pdf");
        evt.SizeBytes.ShouldBe(4_500_000L);
    }

    [Fact]
    public void BlobValidatedEvent_ImplementsIDomainEvent()
    {
        BlobValidatedEvent evt = new(Guid.NewGuid(), "c", "t", 0);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobRejectedEvent ────────────────────────────────────────────────────

    [Fact]
    public void BlobRejectedEvent_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();

        BlobRejectedEvent evt = new(blobId, "images", "MIME mismatch");

        evt.BlobId.ShouldBe(blobId);
        evt.ContainerName.ShouldBe("images");
        evt.RejectionReason.ShouldBe("MIME mismatch");
    }

    [Fact]
    public void BlobRejectedEvent_ImplementsIDomainEvent()
    {
        BlobRejectedEvent evt = new(Guid.NewGuid(), "c", "r");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobDeletedEvent ─────────────────────────────────────────────────────

    [Fact]
    public void BlobDeletedEvent_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();

        BlobDeletedEvent evt = new(blobId, "docs", "RGPD Art. 17");

        evt.BlobId.ShouldBe(blobId);
        evt.ContainerName.ShouldBe("docs");
        evt.DeletionReason.ShouldBe("RGPD Art. 17");
    }

    [Fact]
    public void BlobDeletedEvent_WithNullReason_SetsNull()
    {
        BlobDeletedEvent evt = new(Guid.NewGuid(), "docs", null);

        evt.DeletionReason.ShouldBeNull();
    }

    [Fact]
    public void BlobDeletedEvent_ImplementsIDomainEvent()
    {
        BlobDeletedEvent evt = new(Guid.NewGuid(), "c", null);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }
}
