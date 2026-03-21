using Granit.Timeline.Domain;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineAttachmentTests
{
    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var id = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blobId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        TimelineAttachment attachment = new()
        {
            Id = id,
            EntryId = entryId,
            BlobId = blobId,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 4096,
            TenantId = tenantId,
        };

        attachment.Id.ShouldBe(id);
        attachment.EntryId.ShouldBe(entryId);
        attachment.BlobId.ShouldBe(blobId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.SizeBytes.ShouldBe(4096);
        attachment.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        TimelineAttachment attachment = new();

        attachment.FileName.ShouldBe(string.Empty);
        attachment.ContentType.ShouldBe(string.Empty);
        attachment.SizeBytes.ShouldBe(0);
        attachment.TenantId.ShouldBeNull();
    }
}
