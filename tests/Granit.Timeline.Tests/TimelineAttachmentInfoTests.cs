using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineAttachmentInfoTests
{

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var blobId = Guid.NewGuid();

        TimelineAttachmentInfo a = new(id, blobId, "file.txt", "text/plain", 100);
        TimelineAttachmentInfo b = new(id, blobId, "file.txt", "text/plain", 100);

        a.ShouldBe(b);
    }

    [Fact]
    public void Record_Inequality_DetectsDifferences()
    {
        var id = Guid.NewGuid();
        var blobId = Guid.NewGuid();

        TimelineAttachmentInfo a = new(id, blobId, "file.txt", "text/plain", 100);
        TimelineAttachmentInfo b = new(id, blobId, "other.txt", "text/plain", 100);

        a.ShouldNotBe(b);
    }
}
