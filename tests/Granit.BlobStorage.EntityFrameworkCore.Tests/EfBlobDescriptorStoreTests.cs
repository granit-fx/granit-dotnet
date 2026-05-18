using Granit.BlobStorage.Domain;
using Granit.BlobStorage.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.EntityFrameworkCore.Tests;

public sealed class EfBlobDescriptorStoreTests
{
    // =========================================================================
    // Test infrastructure
    // =========================================================================

    private sealed class InMemoryContextFactory(string dbName, ICurrentTenant? currentTenant = null)
        : IDbContextFactory<BlobStorageDbContext>
    {
        public BlobStorageDbContext CreateDbContext()
        {
            DbContextOptions<BlobStorageDbContext> options =
                new DbContextOptionsBuilder<BlobStorageDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;
            return new BlobStorageDbContext(options, currentTenant ?? GranitDesignTime.CurrentTenant);
        }

        public Task<BlobStorageDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ICurrentTenant MakeTenant(Guid id)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(id);
        return tenant;
    }

    private static async Task MarkAsValid(EfBlobDescriptorStore store, Guid blobId)
    {
        BlobDescriptor? d = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        d!.MarkAsUploading();
        await store.UpdateAsync(d, TestContext.Current.CancellationToken);
        BlobDescriptor? u = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        u!.MarkAsValid("application/pdf", 1024L, DateTimeOffset.UtcNow);
        await store.UpdateAsync(u, TestContext.Current.CancellationToken);
    }

    private static EfBlobDescriptorStore CreateStore(string dbName, Guid? tenantId = null)
    {
        ICurrentTenant tenant = MakeTenant(tenantId ?? TenantId);
        return new(new InMemoryContextFactory(dbName, tenant), tenant);
    }

    private static BlobDescriptor MakeDescriptor(
        Guid? id = null,
        Guid? tenantId = null,
        string containerName = "prescriptions",
        DateTimeOffset? createdAt = null)
    {
        Guid tid = tenantId ?? TenantId;
        Guid bid = id ?? Guid.NewGuid();
        return BlobDescriptor.Create(
            id: bid,
            tenantId: tid,
            containerName: containerName,
            objectKey: $"{tid}/{containerName}/2026/02/{bid}",
            request: new BlobUploadRequest("prescription.pdf", "application/pdf", 5_000_000L),
            createdAt: createdAt ?? new DateTimeOffset(2026, 2, 23, 10, 0, 0, TimeSpan.Zero));
    }

    // =========================================================================
    // FindAsync — not found
    // =========================================================================

    [Fact]
    public async Task FindAsync_UnknownBlob_ReturnsNull()
    {
        EfBlobDescriptorStore store = CreateStore(Guid.NewGuid().ToString());

        BlobDescriptor? result = await store.FindAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // =========================================================================
    // SaveAsync + FindAsync — roundtrip
    // =========================================================================

    [Fact]
    public async Task SaveAsync_PersistsDescriptor_CanBeRetrievedByBlobId()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptor = MakeDescriptor(id: blobId);

        await store.SaveAsync(descriptor, TestContext.Current.CancellationToken);

        BlobDescriptor? retrieved = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved!.Id.ShouldBe(blobId);
        retrieved.Status.ShouldBe(BlobStatus.Pending);
        retrieved.OriginalFileName.ShouldBe("prescription.pdf");
        retrieved.DeclaredContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task SaveAsync_PreservesAllMandatoryFields()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        DateTimeOffset createdAt = new(2026, 2, 23, 10, 0, 0, TimeSpan.Zero);
        var descriptor = BlobDescriptor.Create(
            id: blobId,
            tenantId: TenantId,
            containerName: "medical-images",
            objectKey: $"{TenantId}/medical-images/2026/02/{blobId}",
            request: new BlobUploadRequest("scan.dcm", "application/dicom", 20_000_000L),
            createdAt: createdAt);

        await store.SaveAsync(descriptor, TestContext.Current.CancellationToken);

        BlobDescriptor? retrieved = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        retrieved!.TenantId.ShouldBe(TenantId);
        retrieved.ContainerName.ShouldBe("medical-images");
        retrieved.ObjectKey.ShouldBe($"{TenantId}/medical-images/2026/02/{blobId}");
        retrieved.OriginalFileName.ShouldBe("scan.dcm");
        retrieved.DeclaredContentType.ShouldBe("application/dicom");
        retrieved.CreatedAt.ShouldBe(createdAt);
    }

    // =========================================================================
    // UpdateAsync — state transitions
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_AfterMarkAsUploading_PersistsNewStatus()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        await store.SaveAsync(MakeDescriptor(id: blobId), TestContext.Current.CancellationToken);

        BlobDescriptor? descriptor = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        descriptor!.MarkAsUploading();
        await store.UpdateAsync(descriptor, TestContext.Current.CancellationToken);

        BlobDescriptor? updated = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        updated!.Status.ShouldBe(BlobStatus.Uploading);
    }

    [Fact]
    public async Task UpdateAsync_AfterMarkAsValid_PersistsVerifiedTypeAndSize()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        await store.SaveAsync(MakeDescriptor(id: blobId), TestContext.Current.CancellationToken);

        BlobDescriptor? descriptor = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        descriptor!.MarkAsUploading();
        await store.UpdateAsync(descriptor, TestContext.Current.CancellationToken);

        BlobDescriptor? uploading = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        DateTimeOffset validatedAt = new(2026, 2, 23, 10, 5, 0, TimeSpan.Zero);
        uploading!.MarkAsValid("application/pdf", 204_800L, validatedAt);
        await store.UpdateAsync(uploading, TestContext.Current.CancellationToken);

        BlobDescriptor? valid = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        valid!.Status.ShouldBe(BlobStatus.Valid);
        valid.VerifiedContentType.ShouldBe("application/pdf");
        valid.SizeBytes.ShouldBe(204_800L);
        valid.ValidatedAt.ShouldBe(validatedAt);
    }

    [Fact]
    public async Task UpdateAsync_AfterMarkAsRejected_PersistsRejectionReason()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        await store.SaveAsync(MakeDescriptor(id: blobId), TestContext.Current.CancellationToken);

        BlobDescriptor? descriptor = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        descriptor!.MarkAsUploading();
        await store.UpdateAsync(descriptor, TestContext.Current.CancellationToken);

        BlobDescriptor? uploading = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        uploading!.MarkAsRejected("Invalid magic bytes: expected PDF signature.");
        await store.UpdateAsync(uploading, TestContext.Current.CancellationToken);

        BlobDescriptor? rejected = await store.FindAsync(
            blobId, TestContext.Current.CancellationToken);
        rejected!.Status.ShouldBe(BlobStatus.Rejected);
        rejected.RejectionReason.ShouldBe("Invalid magic bytes: expected PDF signature.");
    }

    [Fact]
    public async Task UpdateAsync_AfterMarkAsDeleted_PreservesAuditRecordInDatabase()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        var blobId = Guid.NewGuid();
        await store.SaveAsync(MakeDescriptor(id: blobId), TestContext.Current.CancellationToken);

        // Advance through Pending -> Uploading -> Valid -> Deleted.
        BlobDescriptor? pending = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        pending!.MarkAsUploading();
        await store.UpdateAsync(pending, TestContext.Current.CancellationToken);

        BlobDescriptor? uploading = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        uploading!.MarkAsValid("application/pdf", 512L, DateTimeOffset.UtcNow);
        await store.UpdateAsync(uploading, TestContext.Current.CancellationToken);

        BlobDescriptor? valid = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        DateTimeOffset deletedAt = new(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);
        valid!.MarkAsDeleted(deletedAt, "GDPR Art. 17 erasure request");
        await store.UpdateAsync(valid, TestContext.Current.CancellationToken);

        // GDPR /ISO 27001: the audit row must remain in the database after deletion.
        BlobDescriptor? deleted = await store.FindAsync(blobId, TestContext.Current.CancellationToken);
        deleted.ShouldNotBeNull("ISO 27001 requires the audit record to be retained for 3 years");
        deleted!.Status.ShouldBe(BlobStatus.Deleted);
        deleted.DeletedAt.ShouldBe(deletedAt);
        deleted.DeletionReason.ShouldBe("GDPR Art. 17 erasure request");
    }

    // =========================================================================
    // Tenant isolation
    // =========================================================================

    [Fact]
    public async Task FindAsync_DoesNotReturnDescriptorBelongingToAnotherTenant()
    {
        string db = Guid.NewGuid().ToString();
        // Save a descriptor for OtherTenant.
        EfBlobDescriptorStore storeOther = CreateStore(db, OtherTenantId);
        var blobId = Guid.NewGuid();
        BlobDescriptor descriptorOther = MakeDescriptor(
            id: blobId, tenantId: OtherTenantId);
        await storeOther.SaveAsync(descriptorOther, TestContext.Current.CancellationToken);

        // Attempt to retrieve it as TenantId (current tenant) -> must return null.
        EfBlobDescriptorStore storeTenant = CreateStore(db, TenantId);
        BlobDescriptor? result = await storeTenant.FindAsync(
            blobId, TestContext.Current.CancellationToken);

        result.ShouldBeNull("cross-tenant access must be blocked at the store level");
    }

    // =========================================================================
    // No active tenant
    // =========================================================================

    [Fact]
    public async Task FindAsync_WithNoActiveTenant_ReturnsNull()
    {
        ICurrentTenant noTenant = Substitute.For<ICurrentTenant>();
        noTenant.IsAvailable.Returns(false);
        noTenant.Id.Returns((Guid?)null);
        EfBlobDescriptorStore store = new(
            new InMemoryContextFactory(Guid.NewGuid().ToString(), noTenant), noTenant);

        BlobDescriptor? result = await store.FindAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull("no blob exists with matching TenantId");
    }

    // =========================================================================
    // FindOrphanedAsync
    // =========================================================================

    [Fact]
    public async Task FindOrphanedAsync_ReturnsPendingAndUploadingBeforeCutoff()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset recent = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        // Old pending blob — should be returned
        BlobDescriptor oldPending = MakeDescriptor(createdAt: old);
        await store.SaveAsync(oldPending, TestContext.Current.CancellationToken);

        // Old uploading blob — should be returned
        BlobDescriptor oldUploading = MakeDescriptor(createdAt: old);
        await store.SaveAsync(oldUploading, TestContext.Current.CancellationToken);
        BlobDescriptor? toUpload = await store.FindAsync(oldUploading.Id, TestContext.Current.CancellationToken);
        toUpload!.MarkAsUploading();
        await store.UpdateAsync(toUpload, TestContext.Current.CancellationToken);

        // Recent pending blob — should NOT be returned (after cutoff)
        BlobDescriptor recentPending = MakeDescriptor(createdAt: recent);
        await store.SaveAsync(recentPending, TestContext.Current.CancellationToken);

        // Old valid blob — should NOT be returned (not orphaned)
        BlobDescriptor oldValid = MakeDescriptor(createdAt: old);
        await store.SaveAsync(oldValid, TestContext.Current.CancellationToken);
        BlobDescriptor? toValidate = await store.FindAsync(oldValid.Id, TestContext.Current.CancellationToken);
        toValidate!.MarkAsUploading();
        await store.UpdateAsync(toValidate, TestContext.Current.CancellationToken);
        BlobDescriptor? uploading = await store.FindAsync(oldValid.Id, TestContext.Current.CancellationToken);
        uploading!.MarkAsValid("application/pdf", 1024L, old);
        await store.UpdateAsync(uploading, TestContext.Current.CancellationToken);

        IReadOnlyList<BlobDescriptor> orphans = await store.FindOrphanedAsync(
            cutoff, 100, TestContext.Current.CancellationToken);

        orphans.Count.ShouldBe(2);
        orphans.ShouldAllBe(b => b.Status == BlobStatus.Pending || b.Status == BlobStatus.Uploading);
        orphans.ShouldAllBe(b => b.CreatedAt < cutoff);
    }

    [Fact]
    public async Task FindOrphanedAsync_RespectsPageSize()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 5; i++)
        {
            await store.SaveAsync(MakeDescriptor(createdAt: old), TestContext.Current.CancellationToken);
        }

        IReadOnlyList<BlobDescriptor> orphans = await store.FindOrphanedAsync(
            cutoff, 3, TestContext.Current.CancellationToken);

        orphans.Count.ShouldBe(3);
    }

    [Fact]
    public async Task FindOrphanedAsync_EmptyWhenNoOrphans()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        IReadOnlyList<BlobDescriptor> orphans = await store.FindOrphanedAsync(
            cutoff, 100, TestContext.Current.CancellationToken);

        orphans.ShouldBeEmpty();
    }

    // =========================================================================
    // FindByContainerBeforeAsync
    // =========================================================================

    [Fact]
    public async Task FindByContainerBeforeAsync_FiltersContainerAndCutoff()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset recent = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        // Old valid blob in target container — should be returned
        BlobDescriptor match = MakeDescriptor(containerName: "prescriptions", createdAt: old);
        await store.SaveAsync(match, TestContext.Current.CancellationToken);
        await MarkAsValid(store, match.Id);

        // Old valid blob in different container — should NOT be returned
        BlobDescriptor otherContainer = MakeDescriptor(containerName: "avatars", createdAt: old);
        await store.SaveAsync(otherContainer, TestContext.Current.CancellationToken);
        await MarkAsValid(store, otherContainer.Id);

        // Recent valid blob in target container — should NOT be returned (after cutoff)
        BlobDescriptor recentBlob = MakeDescriptor(containerName: "prescriptions", createdAt: recent);
        await store.SaveAsync(recentBlob, TestContext.Current.CancellationToken);
        await MarkAsValid(store, recentBlob.Id);

        // Old pending blob in target container — should NOT be returned (not Valid)
        BlobDescriptor pendingBlob = MakeDescriptor(containerName: "prescriptions", createdAt: old);
        await store.SaveAsync(pendingBlob, TestContext.Current.CancellationToken);

        IReadOnlyList<BlobDescriptor> results = await store.FindByContainerBeforeAsync(
            "prescriptions", cutoff, 100, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task FindByContainerBeforeAsync_RespectsPageSize()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 5; i++)
        {
            BlobDescriptor blob = MakeDescriptor(containerName: "prescriptions", createdAt: old);
            await store.SaveAsync(blob, TestContext.Current.CancellationToken);
            await MarkAsValid(store, blob.Id);
        }

        IReadOnlyList<BlobDescriptor> results = await store.FindByContainerBeforeAsync(
            "prescriptions", cutoff, 3, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
    }

    [Fact]
    public async Task FindByContainerBeforeAsync_EmptyWhenNoMatch()
    {
        string db = Guid.NewGuid().ToString();
        EfBlobDescriptorStore store = CreateStore(db);
        DateTimeOffset cutoff = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        IReadOnlyList<BlobDescriptor> results = await store.FindByContainerBeforeAsync(
            "prescriptions", cutoff, 100, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }
}
