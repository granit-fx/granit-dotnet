using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Events;
using Granit.Core.Events;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobDescriptorTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static BlobDescriptor CreatePending() => BlobDescriptor.Create(
        id: Guid.NewGuid(),
        tenantId: TestTenantId,
        containerName: "medical-images",
        objectKey: $"{TestTenantId}/medical-images/2026/02/some-guid",
        request: new BlobUploadRequest("radio.jpg", "image/jpeg", 10_000_000L),
        createdAt: Now);

    // ── Factory ─────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldReturnPendingDescriptorWithCorrectFields()
    {
        var id = Guid.NewGuid();

        var descriptor = BlobDescriptor.Create(
            id: id,
            tenantId: TestTenantId,
            containerName: "medical-images",
            objectKey: $"{TestTenantId}/medical-images/2026/02/some-guid",
            request: new BlobUploadRequest("radio.jpg", "image/jpeg", 10_000_000L),
            createdAt: Now);

        descriptor.Id.ShouldBe(id);
        descriptor.TenantId.ShouldBe(TestTenantId);
        descriptor.ContainerName.ShouldBe("medical-images");
        descriptor.Status.ShouldBe(BlobStatus.Pending);
        descriptor.OriginalFileName.ShouldBe("radio.jpg");
        descriptor.DeclaredContentType.ShouldBe("image/jpeg");
        descriptor.CreatedAt.ShouldBe(Now);
        descriptor.VerifiedContentType.ShouldBeNull();
        descriptor.SizeBytes.ShouldBeNull();
        descriptor.ValidatedAt.ShouldBeNull();
        descriptor.DeletedAt.ShouldBeNull();
        descriptor.RejectionReason.ShouldBeNull();
        descriptor.DeletionReason.ShouldBeNull();
    }

    // ── Pending → Uploading ──────────────────────────────────────────────────

    [Fact]
    public void MarkAsUploading_FromPending_ShouldTransitionToUploading()
    {
        BlobDescriptor descriptor = CreatePending();

        descriptor.MarkAsUploading();

        descriptor.Status.ShouldBe(BlobStatus.Uploading);
    }

    [Theory]
    [InlineData(BlobStatus.Uploading)]
    [InlineData(BlobStatus.Valid)]
    [InlineData(BlobStatus.Rejected)]
    [InlineData(BlobStatus.Deleted)]
    public void MarkAsUploading_FromIllegalStatus_ShouldThrow(BlobStatus illegalStatus)
    {
        BlobDescriptor descriptor = BuildDescriptorInStatus(illegalStatus);

        Action act = () => descriptor.MarkAsUploading();

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain($"{illegalStatus}");
    }

    // ── Uploading → Valid ────────────────────────────────────────────────────

    [Fact]
    public void MarkAsValid_FromUploading_ShouldTransitionToValid()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        DateTimeOffset validatedAt = Now.AddMinutes(2);

        descriptor.MarkAsValid("image/jpeg", 512_000, validatedAt);

        descriptor.Status.ShouldBe(BlobStatus.Valid);
        descriptor.VerifiedContentType.ShouldBe("image/jpeg");
        descriptor.SizeBytes.ShouldBe(512_000);
        descriptor.ValidatedAt.ShouldBe(validatedAt);
    }

    [Theory]
    [InlineData(BlobStatus.Pending)]
    [InlineData(BlobStatus.Valid)]
    [InlineData(BlobStatus.Rejected)]
    [InlineData(BlobStatus.Deleted)]
    public void MarkAsValid_FromIllegalStatus_ShouldThrow(BlobStatus illegalStatus)
    {
        BlobDescriptor descriptor = BuildDescriptorInStatus(illegalStatus);

        Action act = () => descriptor.MarkAsValid("image/jpeg", 512_000, Now);

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain($"{illegalStatus}");
    }

    // ── Uploading → Rejected ─────────────────────────────────────────────────

    [Fact]
    public void MarkAsRejected_FromUploading_ShouldTransitionToRejected()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();

        descriptor.MarkAsRejected("MIME mismatch: declared image/jpeg but magic bytes are MZ (executable).");

        descriptor.Status.ShouldBe(BlobStatus.Rejected);
        descriptor.RejectionReason!.ShouldContain("MZ");
    }

    [Theory]
    [InlineData(BlobStatus.Pending)]
    [InlineData(BlobStatus.Valid)]
    [InlineData(BlobStatus.Rejected)]
    [InlineData(BlobStatus.Deleted)]
    public void MarkAsRejected_FromIllegalStatus_ShouldThrow(BlobStatus illegalStatus)
    {
        BlobDescriptor descriptor = BuildDescriptorInStatus(illegalStatus);

        Action act = () => descriptor.MarkAsRejected("some reason");

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain($"{illegalStatus}");
    }

    // ── Valid → Deleted (Crypto-Shredding) ───────────────────────────────────

    [Fact]
    public void MarkAsDeleted_FromValid_ShouldTransitionToDeletedAndPreserveAuditFields()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now.AddMinutes(1));
        DateTimeOffset deletedAt = Now.AddDays(30);

        descriptor.MarkAsDeleted(deletedAt, "RGPD Art. 17 erasure request");

        descriptor.Status.ShouldBe(BlobStatus.Deleted);
        descriptor.DeletedAt.ShouldBe(deletedAt);
        descriptor.DeletionReason.ShouldBe("RGPD Art. 17 erasure request");
        // Audit fields must be preserved — the DB record is never removed.
        descriptor.Id.ShouldNotBe(Guid.Empty);
        descriptor.TenantId.ShouldBe(TestTenantId);
        descriptor.OriginalFileName.ShouldBe("radio.jpg");
        descriptor.ValidatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkAsDeleted_WithNoReason_ShouldSucceed()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now);

        descriptor.MarkAsDeleted(Now.AddDays(1));

        descriptor.Status.ShouldBe(BlobStatus.Deleted);
        descriptor.DeletionReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(BlobStatus.Pending)]
    [InlineData(BlobStatus.Uploading)]
    [InlineData(BlobStatus.Rejected)]
    [InlineData(BlobStatus.Deleted)]
    public void MarkAsDeleted_FromIllegalStatus_ShouldThrow(BlobStatus illegalStatus)
    {
        BlobDescriptor descriptor = BuildDescriptorInStatus(illegalStatus);

        Action act = () => descriptor.MarkAsDeleted(Now);

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain($"{illegalStatus}");
    }

    // ── Domain Events ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldNotEmitAnyDomainEvent()
    {
        BlobDescriptor descriptor = CreatePending();

        descriptor.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkAsUploading_ShouldEmitBlobUploadStartedEvent()
    {
        BlobDescriptor descriptor = CreatePending();

        descriptor.MarkAsUploading();

        BlobUploadStartedEvent evt = descriptor.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BlobUploadStartedEvent>();
        evt.BlobId.ShouldBe(descriptor.Id);
        evt.ContainerName.ShouldBe("medical-images");
        evt.OriginalFileName.ShouldBe("radio.jpg");
    }

    [Fact]
    public void MarkAsValid_ShouldEmitBlobValidatedEventEvent()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.ClearDomainEvents();

        descriptor.MarkAsValid("image/jpeg", 512_000, Now.AddMinutes(2));

        BlobValidatedEvent evt = descriptor.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BlobValidatedEvent>();
        evt.BlobId.ShouldBe(descriptor.Id);
        evt.ContainerName.ShouldBe("medical-images");
        evt.VerifiedContentType.ShouldBe("image/jpeg");
        evt.SizeBytes.ShouldBe(512_000);
    }

    [Fact]
    public void MarkAsRejected_ShouldEmitBlobRejectedEventEvent()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.ClearDomainEvents();

        descriptor.MarkAsRejected("MIME mismatch");

        BlobRejectedEvent evt = descriptor.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BlobRejectedEvent>();
        evt.BlobId.ShouldBe(descriptor.Id);
        evt.ContainerName.ShouldBe("medical-images");
        evt.RejectionReason.ShouldBe("MIME mismatch");
    }

    [Fact]
    public void MarkAsDeleted_ShouldEmitBlobDeletedEventEvent()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now);
        descriptor.ClearDomainEvents();

        descriptor.MarkAsDeleted(Now.AddDays(1), "RGPD Art. 17");

        BlobDeletedEvent evt = descriptor.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BlobDeletedEvent>();
        evt.BlobId.ShouldBe(descriptor.Id);
        evt.ContainerName.ShouldBe("medical-images");
        evt.DeletionReason.ShouldBe("RGPD Art. 17");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllCollectedEvents()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now);
        descriptor.DomainEvents.ShouldNotBeEmpty();

        descriptor.ClearDomainEvents();

        descriptor.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void FullLifecycle_ShouldAccumulateAllEvents()
    {
        BlobDescriptor descriptor = CreatePending();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 512_000, Now);
        descriptor.MarkAsDeleted(Now.AddDays(1), "cleanup");

        descriptor.DomainEvents.Count.ShouldBe(3);
        descriptor.DomainEvents.ShouldContain(e => e is BlobUploadStartedEvent);
        descriptor.DomainEvents.ShouldContain(e => e is BlobValidatedEvent);
        descriptor.DomainEvents.ShouldContain(e => e is BlobDeletedEvent);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static BlobDescriptor BuildDescriptorInStatus(BlobStatus target)
    {
        BlobDescriptor descriptor = CreatePending();
        switch (target)
        {
            case BlobStatus.Pending:
                break;
            case BlobStatus.Uploading:
                descriptor.MarkAsUploading();
                break;
            case BlobStatus.Valid:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("image/jpeg", 512_000, Now);
                break;
            case BlobStatus.Rejected:
                descriptor.MarkAsUploading();
                descriptor.MarkAsRejected("test rejection");
                break;
            case BlobStatus.Deleted:
                descriptor.MarkAsUploading();
                descriptor.MarkAsValid("image/jpeg", 512_000, Now);
                descriptor.MarkAsDeleted(Now.AddDays(1));
                break;
        }
        return descriptor;
    }
}
