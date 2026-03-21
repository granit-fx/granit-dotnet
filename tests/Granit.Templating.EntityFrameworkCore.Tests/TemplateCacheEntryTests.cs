using Granit.Templating.EntityFrameworkCore.Internal;
using Granit.Templating.Pipeline;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class TemplateCacheEntryTests
{
    [Fact]
    public void NotFound_IsNotFound()
    {
        TemplateCacheEntry entry = TemplateCacheEntry.NotFound;

        entry.IsFound.ShouldBeFalse();
        entry.Content.ShouldBeNull();
        entry.MimeType.ShouldBeNull();
        entry.RevisionId.ShouldBeNull();
    }

    [Fact]
    public void NotFound_ToDescriptor_ReturnsNull()
    {
        TemplateCacheEntry entry = TemplateCacheEntry.NotFound;

        TemplateDescriptor? descriptor = entry.ToDescriptor();

        descriptor.ShouldBeNull();
    }

    [Fact]
    public void From_CreatesFoundEntry()
    {
        var revisionId = Guid.NewGuid();
        var entry = TemplateCacheEntry.From("<p>Hello</p>", "text/html", revisionId);

        entry.IsFound.ShouldBeTrue();
        entry.Content.ShouldBe("<p>Hello</p>");
        entry.MimeType.ShouldBe("text/html");
        entry.RevisionId.ShouldBe(revisionId);
    }

    [Fact]
    public void From_ToDescriptor_ReturnsDescriptorWithCorrectProperties()
    {
        var revisionId = Guid.NewGuid();
        var entry = TemplateCacheEntry.From("<p>Content</p>", "text/html", revisionId);

        TemplateDescriptor? descriptor = entry.ToDescriptor();

        descriptor.ShouldNotBeNull();
        descriptor!.Content.ShouldBe("<p>Content</p>");
        descriptor.MimeType.ShouldBe("text/html");
        descriptor.RevisionId.ShouldBe(revisionId);
    }

    [Fact]
    public void From_WithNullRevisionId_ToDescriptor_HasNullRevisionId()
    {
        var entry = TemplateCacheEntry.From("<p>No revision</p>", "text/html", null);

        TemplateDescriptor? descriptor = entry.ToDescriptor();

        descriptor.ShouldNotBeNull();
        descriptor!.RevisionId.ShouldBeNull();
    }

    [Fact]
    public void NotFound_IsSingleton()
    {
        TemplateCacheEntry entry1 = TemplateCacheEntry.NotFound;
        TemplateCacheEntry entry2 = TemplateCacheEntry.NotFound;

        entry1.ShouldBeSameAs(entry2);
    }
}
