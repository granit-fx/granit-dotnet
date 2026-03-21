using Granit.Webhooks.Definitions;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Endpoints;

public sealed class WebhookEventTypeEndpointsTests
{
    [Fact]
    public void GetEventTypes_EmptyRegistry_ReturnsEmptyList()
    {
        IWebhookEventTypeRegistry registry = Substitute.For<IWebhookEventTypeRegistry>();
        registry.GetAll().Returns([]);

        Ok<IReadOnlyList<WebhookEventTypeResponse>> result = WebhookEventTypeEndpoints.GetEventTypes(registry);

        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void GetEventTypes_WithRegisteredTypes_ReturnsMappedResponses()
    {
        IWebhookEventTypeRegistry registry = Substitute.For<IWebhookEventTypeRegistry>();
        registry.GetAll().Returns([
            new WebhookEventTypeDefinition("document.uploaded", "Document uploaded", "Fires on upload", "Documents"),
            new WebhookEventTypeDefinition("patient.created", "Patient created", null, "Patients"),
        ]);

        Ok<IReadOnlyList<WebhookEventTypeResponse>> result = WebhookEventTypeEndpoints.GetEventTypes(registry);

        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);

        result.Value[0].EventType.ShouldBe("document.uploaded");
        result.Value[0].DisplayName.ShouldBe("Document uploaded");
        result.Value[0].Description.ShouldBe("Fires on upload");
        result.Value[0].Category.ShouldBe("Documents");

        result.Value[1].EventType.ShouldBe("patient.created");
        result.Value[1].DisplayName.ShouldBe("Patient created");
        result.Value[1].Description.ShouldBeNull();
        result.Value[1].Category.ShouldBe("Patients");
    }
}
