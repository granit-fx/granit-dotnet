using Granit.Webhooks.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests;

public sealed class WebhooksEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_Default_ShouldBeWebhooks() =>
        new WebhooksEndpointsOptions().RoutePrefix.ShouldBe("webhooks");

    [Fact]
    public void RequiredRole_Default_ShouldBeGranitWebhooksAdmin() =>
        new WebhooksEndpointsOptions().RequiredRole.ShouldBe("granit-webhooks-admin");

    [Fact]
    public void TagName_Default_ShouldBeWebhooks() =>
        new WebhooksEndpointsOptions().TagName.ShouldBe("Webhooks");

    [Fact]
    public void SectionName_ShouldBeWebhooksEndpoints() =>
        WebhooksEndpointsOptions.SectionName.ShouldBe("WebhooksEndpoints");
}
