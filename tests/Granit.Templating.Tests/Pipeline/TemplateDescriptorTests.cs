using Granit.Templating.Pipeline;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Pipeline;

public sealed class TemplateDescriptorTests
{
    [Fact]
    public void RequiredProperties_CanBeSet()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "<p>Hello {{ model.name }}</p>",
            MimeType = "text/html",
        };

        descriptor.Content.ShouldBe("<p>Hello {{ model.name }}</p>");
        descriptor.MimeType.ShouldBe("text/html");
    }

    [Fact]
    public void RevisionId_DefaultsToNull()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "content",
            MimeType = "text/html",
        };

        descriptor.RevisionId.ShouldBeNull();
    }

    [Fact]
    public void RevisionId_CanBeSet()
    {
        var revisionId = Guid.NewGuid();
        TemplateDescriptor descriptor = new()
        {
            Content = "content",
            MimeType = "text/html",
            RevisionId = revisionId,
        };

        descriptor.RevisionId.ShouldBe(revisionId);
    }
}
