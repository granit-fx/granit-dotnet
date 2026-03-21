using Granit.Webhooks.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Dtos;

public sealed class WebhookEventTypeResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var response = new WebhookEventTypeResponse(
            "document.uploaded",
            "Document uploaded",
            "Fires when a document is uploaded",
            "Documents");

        response.EventType.ShouldBe("document.uploaded");
        response.DisplayName.ShouldBe("Document uploaded");
        response.Description.ShouldBe("Fires when a document is uploaded");
        response.Category.ShouldBe("Documents");
    }

    [Fact]
    public void Constructor_NullableFields_CanBeNull()
    {
        var response = new WebhookEventTypeResponse("patient.created", null, null, null);

        response.EventType.ShouldBe("patient.created");
        response.DisplayName.ShouldBeNull();
        response.Description.ShouldBeNull();
        response.Category.ShouldBeNull();
    }
}
