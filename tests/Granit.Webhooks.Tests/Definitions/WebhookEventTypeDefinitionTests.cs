using Granit.Webhooks.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookEventTypeDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var definition = new WebhookEventTypeDefinition(
            "document.uploaded", "Document uploaded", "Fires when a document is uploaded", "Documents");

        definition.Name.ShouldBe("document.uploaded");
        definition.DisplayName.ShouldBe("Document uploaded");
        definition.Description.ShouldBe("Fires when a document is uploaded");
        definition.Category.ShouldBe("Documents");
    }

    [Fact]
    public void Constructor_OptionalParametersDefaultToNull()
    {
        var definition = new WebhookEventTypeDefinition("patient.created");

        definition.Name.ShouldBe("patient.created");
        definition.DisplayName.ShouldBeNull();
        definition.Description.ShouldBeNull();
        definition.Category.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new WebhookEventTypeDefinition("doc.uploaded", "Doc", "Desc", "Cat");
        var b = new WebhookEventTypeDefinition("doc.uploaded", "Doc", "Desc", "Cat");

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentNames_AreNotEqual()
    {
        var a = new WebhookEventTypeDefinition("doc.uploaded");
        var b = new WebhookEventTypeDefinition("doc.deleted");

        a.ShouldNotBe(b);
    }
}
