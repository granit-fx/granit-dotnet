using Granit.Localization;
using Granit.Webhooks.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookEventTypeDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var displayName = LocalizableString.Fixed("Document uploaded");
        var description = LocalizableString.Fixed("Fires when a document is uploaded");
        var category = LocalizableString.Fixed("Documents");

        var definition = new WebhookEventTypeDefinition(
            "document.uploaded", displayName, description, category);

        definition.Name.ShouldBe("document.uploaded");
        definition.DisplayName.ShouldBe(displayName);
        definition.Description.ShouldBe(description);
        definition.Category.ShouldBe(category);
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
    public void LocalizableString_Fixed_ResolvesWithoutFactory()
    {
        var displayName = LocalizableString.Fixed("My label");

        var definition = new WebhookEventTypeDefinition("test.event", displayName);

        definition.DisplayName!.Localize(null).ShouldBe("My label");
    }

    [Fact]
    public void LocalizableString_Create_FallsBackToKeyWithoutFactory()
    {
        var displayName = LocalizableString.Create<WebhookEventTypeDefinitionTests>(
            "WebhookEventType:test.event");

        var definition = new WebhookEventTypeDefinition("test.event", displayName);

        definition.DisplayName!.Localize(null).ShouldBe("WebhookEventType:test.event");
    }
}
