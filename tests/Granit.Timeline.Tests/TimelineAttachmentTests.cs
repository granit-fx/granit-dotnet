using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineAttachmentTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blobId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        var attachment = TimelineAttachment.Create(
            id, entryId, blobId,
            new FileMetadata("report.pdf", "application/pdf", 4096),
            createdAt, "admin@test.com", tenantId);

        attachment.Id.ShouldBe(id);
        attachment.EntryId.ShouldBe(entryId);
        attachment.BlobId.ShouldBe(blobId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.SizeBytes.ShouldBe(4096);
        attachment.CreatedAt.ShouldBe(createdAt);
        attachment.CreatedBy.ShouldBe("admin@test.com");
        attachment.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Create_WithoutTenantId_DefaultsToNull()
    {
        var attachment = TimelineAttachment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new FileMetadata("file.txt", "text/plain", 128),
            DateTimeOffset.UtcNow, "user@test.com");

        attachment.TenantId.ShouldBeNull();
    }
}
