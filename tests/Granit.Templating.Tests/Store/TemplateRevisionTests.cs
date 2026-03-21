using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateRevisionTests
{
    [Fact]
    public void AllRequiredProperties_CanBeSet()
    {
        var revisionId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        TemplateRevision revision = new()
        {
            RevisionId = revisionId,
            Content = "<p>Hello</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Published,
            CreatedAt = createdAt,
            CreatedBy = "alice",
        };

        revision.RevisionId.ShouldBe(revisionId);
        revision.Content.ShouldBe("<p>Hello</p>");
        revision.MimeType.ShouldBe("text/html");
        revision.Status.ShouldBe(TemplateLifecycleStatus.Published);
        revision.CreatedAt.ShouldBe(createdAt);
        revision.CreatedBy.ShouldBe("alice");
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        TemplateRevision revision = new()
        {
            RevisionId = Guid.NewGuid(),
            Content = "<p>Draft</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "bob",
        };

        revision.PublishedAt.ShouldBeNull();
        revision.PublishedBy.ShouldBeNull();
    }

    [Fact]
    public void PublishedProperties_CanBeSet()
    {
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;

        TemplateRevision revision = new()
        {
            RevisionId = Guid.NewGuid(),
            Content = "<p>Published</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedBy = "alice",
            PublishedAt = publishedAt,
            PublishedBy = "bob",
        };

        revision.PublishedAt.ShouldBe(publishedAt);
        revision.PublishedBy.ShouldBe("bob");
    }
}
