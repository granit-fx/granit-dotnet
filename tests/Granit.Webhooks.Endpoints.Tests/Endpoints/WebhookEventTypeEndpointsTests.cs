using Granit.Localization;
using Granit.Webhooks.Definitions;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Endpoints;
using Microsoft.AspNetCore.Http;
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
        HttpContext httpContext = CreateHttpContext();

        Ok<IReadOnlyList<WebhookEventTypeResponse>> result =
            WebhookEventTypeEndpoints.GetEventTypes(registry, httpContext);

        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void GetEventTypes_WithRegisteredTypes_ReturnsMappedResponses()
    {
        IWebhookEventTypeRegistry registry = Substitute.For<IWebhookEventTypeRegistry>();
        registry.GetAll().Returns([
            new WebhookEventTypeDefinition(
                "document.uploaded",
                LocalizableString.Fixed("Document uploaded"),
                LocalizableString.Fixed("Fires on upload"),
                LocalizableString.Fixed("Documents")),
            new WebhookEventTypeDefinition(
                "patient.created",
                LocalizableString.Fixed("Patient created"),
                null,
                LocalizableString.Fixed("Patients")),
        ]);
        HttpContext httpContext = CreateHttpContext();

        Ok<IReadOnlyList<WebhookEventTypeResponse>> result =
            WebhookEventTypeEndpoints.GetEventTypes(registry, httpContext);

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

    [Fact]
    public void GetEventTypes_NullLocalizerFactory_FallsBackToKeys()
    {
        IWebhookEventTypeRegistry registry = Substitute.For<IWebhookEventTypeRegistry>();
        registry.GetAll().Returns([
            new WebhookEventTypeDefinition(
                "order.created",
                LocalizableString.Create<WebhookEventTypeEndpointsTests>("WebhookEventType:order.created")),
        ]);
        HttpContext httpContext = CreateHttpContext();

        Ok<IReadOnlyList<WebhookEventTypeResponse>> result =
            WebhookEventTypeEndpoints.GetEventTypes(registry, httpContext);

        result.Value.ShouldNotBeNull();
        result.Value[0].DisplayName.ShouldBe("WebhookEventType:order.created");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext httpContext = new();
        httpContext.RequestServices = Substitute.For<IServiceProvider>();
        return httpContext;
    }
}
