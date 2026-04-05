using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class TemplateRevisionEntityTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var entity = TemplateRevisionEntity.Create(
            id,
            templateName: "Billing.Invoice",
            culture: "fr-BE",
            content: "<p>Facture</p>",
            mimeType: "text/html",
            layoutName: "default",
            categoryId: categoryId,
            versionId: versionId);

        entity.Id.ShouldBe(id);
        entity.RevisionId.ShouldBe(id);
        entity.TemplateName.ShouldBe("Billing.Invoice");
        entity.Culture.ShouldBe("fr-BE");
        entity.Content.ShouldBe("<p>Facture</p>");
        entity.MimeType.ShouldBe("text/html");
        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
        entity.LayoutName.ShouldBe("default");
        entity.CategoryId.ShouldBe(categoryId);
    }

    [Fact]
    public void Create_OptionalProperties_DefaultToNull()
    {
        var entity = TemplateRevisionEntity.Create(
            Guid.NewGuid(),
            templateName: "Test",
            culture: null,
            content: "content",
            mimeType: "text/html");

        entity.Culture.ShouldBeNull();
        entity.PublishedAt.ShouldBeNull();
        entity.PublishedBy.ShouldBeNull();
        entity.CategoryId.ShouldBeNull();
        entity.LayoutName.ShouldBeNull();
    }

    [Fact]
    public void RevisionId_IsAliasForId()
    {
        var id = Guid.NewGuid();
        var entity = TemplateRevisionEntity.Create(
            id, "Test", null, "content", "text/html");

        entity.RevisionId.ShouldBe(entity.Id);
    }

    [Fact]
    public void Publish_SetsPublishedFields()
    {
        var entity = TemplateRevisionEntity.Create(
            Guid.NewGuid(), "Test", null, "content", "text/html");
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;

        entity.Publish("bob", publishedAt);

        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
        entity.PublishedAt.ShouldBe(publishedAt);
        entity.PublishedBy.ShouldBe("bob");
    }

    [Fact]
    public void Archive_SetsArchivedStatus()
    {
        var entity = TemplateRevisionEntity.Create(
            Guid.NewGuid(), "Test", null, "content", "text/html");
        entity.Publish("bob", DateTimeOffset.UtcNow);

        entity.Archive();

        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
    }

    [Fact]
    public void UpdateDraft_UpdatesContentAndMimeType()
    {
        var entity = TemplateRevisionEntity.Create(
            Guid.NewGuid(), "Test", null, "old content", "text/plain");

        entity.UpdateDraft("<p>new content</p>", "text/html", "layout-v2");

        entity.Content.ShouldBe("<p>new content</p>");
        entity.MimeType.ShouldBe("text/html");
        entity.LayoutName.ShouldBe("layout-v2");
    }

    [Fact]
    public void UpdateDraft_OnPublishedEntity_Throws()
    {
        var entity = TemplateRevisionEntity.Create(
            Guid.NewGuid(), "Test", null, "content", "text/html");
        entity.Publish("bob", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            entity.UpdateDraft("new", "text/html", null));
    }
}
