using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Store;

public sealed class TemplateRevisionTests
{
    private const string AnyStamp = "00000000-0000-0000-0000-000000000000";

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
            Status = WorkflowLifecycleStatus.Published,
            Version = 1,
            CreatedAt = createdAt,
            CreatedBy = "alice",
            ConcurrencyStamp = AnyStamp,
        };

        revision.RevisionId.ShouldBe(revisionId);
        revision.Content.ShouldBe("<p>Hello</p>");
        revision.MimeType.ShouldBe("text/html");
        revision.Status.ShouldBe(WorkflowLifecycleStatus.Published);
        revision.Version.ShouldBe(1);
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
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "bob",
            ConcurrencyStamp = AnyStamp,
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
            Status = WorkflowLifecycleStatus.Published,
            Version = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedBy = "alice",
            PublishedAt = publishedAt,
            PublishedBy = "bob",
            ConcurrencyStamp = AnyStamp,
        };

        revision.PublishedAt.ShouldBe(publishedAt);
        revision.PublishedBy.ShouldBe("bob");
    }
}
