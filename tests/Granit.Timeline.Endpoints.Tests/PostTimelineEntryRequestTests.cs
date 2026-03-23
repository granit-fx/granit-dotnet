using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class PostTimelineEntryRequestTests
{
    [Fact]
    public void RequiredProperties_CanBeSet()
    {
        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.Comment,
            Body = "Test body",
        };

        request.EntryType.ShouldBe(TimelineEntryType.Comment);
        request.Body.ShouldBe("Test body");
        request.ParentEntryId.ShouldBeNull();
        request.AttachmentBlobIds.ShouldBeNull();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var parentId = Guid.NewGuid();
        var blobId = Guid.NewGuid();

        PostTimelineEntryRequest request = new()
        {
            EntryType = TimelineEntryType.InternalNote,
            Body = "Note",
            ParentEntryId = parentId,
            AttachmentBlobIds = [blobId],
        };

        request.ParentEntryId.ShouldBe(parentId);
        request.AttachmentBlobIds.ShouldNotBeNull();
        request.AttachmentBlobIds!.Count.ShouldBe(1);
        request.AttachmentBlobIds[0].ShouldBe(blobId);
    }
}
