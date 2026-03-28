using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Store;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class TemplateRevisionEntityTests
{
    [Fact]
    public void AllProperties_CanBeSet()
    {
        var revisionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        DateTimeOffset archivedAt = DateTimeOffset.UtcNow.AddHours(1);

        TemplateRevisionEntity entity = new()
        {
            RevisionId = revisionId,
            TemplateName = "Billing.Invoice",
            Culture = "fr-BE",
            Content = "<p>Facture</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Published,
            CreatedAt = createdAt,
            CreatedBy = "alice",
            PublishedAt = publishedAt,
            PublishedBy = "bob",
            ArchivedAt = archivedAt,
            ArchivedBy = "carol",
            CategoryId = categoryId,
        };

        entity.RevisionId.ShouldBe(revisionId);
        entity.TemplateName.ShouldBe("Billing.Invoice");
        entity.Culture.ShouldBe("fr-BE");
        entity.Content.ShouldBe("<p>Facture</p>");
        entity.MimeType.ShouldBe("text/html");
        entity.Status.ShouldBe(TemplateLifecycleStatus.Published);
        entity.CreatedAt.ShouldBe(createdAt);
        entity.CreatedBy.ShouldBe("alice");
        entity.PublishedAt.ShouldBe(publishedAt);
        entity.PublishedBy.ShouldBe("bob");
        entity.ArchivedAt.ShouldBe(archivedAt);
        entity.ArchivedBy.ShouldBe("carol");
        entity.CategoryId.ShouldBe(categoryId);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        TemplateRevisionEntity entity = new()
        {
            RevisionId = Guid.NewGuid(),
            TemplateName = "Test",
            Content = "content",
            MimeType = "text/html",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "alice",
        };

        entity.Culture.ShouldBeNull();
        entity.PublishedAt.ShouldBeNull();
        entity.PublishedBy.ShouldBeNull();
        entity.ArchivedAt.ShouldBeNull();
        entity.ArchivedBy.ShouldBeNull();
        entity.CategoryId.ShouldBeNull();
    }
}
