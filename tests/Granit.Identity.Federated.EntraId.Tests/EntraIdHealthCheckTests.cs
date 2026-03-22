using System.Net;
using Granit.Identity.Federated.EntraId.HealthChecks;
using Granit.Identity.Federated.EntraId.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdHealthCheckTests
{
    private readonly EntraIdAdminOptions _options = new()
    {
        TenantId = "00000000-0000-0000-0000-000000000000",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        ServicePrincipalObjectId = "00000000-0000-0000-0000-000000000001",
    };

    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_SuccessfulToken_ReturnsHealthy()
    {
        // Arrange
        FakeHandler handler = CreateHandler(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        EntraIdHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Unauthorized_ReturnsUnhealthy()
    {
        // Arrange
        FakeHandler handler = CreateHandler(HttpStatusCode.Unauthorized);
        IHttpClientFactory factory = CreateFactory(handler);
        EntraIdHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("401");
    }

    [Fact]
    public async Task CheckHealthAsync_ServerError_ReturnsDegraded()
    {
        // Arrange
        FakeHandler handler = CreateHandler(HttpStatusCode.InternalServerError);
        IHttpClientFactory factory = CreateFactory(handler);
        EntraIdHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("500");
    }

    [Fact]
    public async Task CheckHealthAsync_Exception_ReturnsUnhealthyWithSanitizedMessage()
    {
        // Arrange
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph")
            .Returns(_ => throw new HttpRequestException("Connection refused to login.microsoftonline.com with secret=xyz"));

        EntraIdHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Entra ID unreachable: HttpRequestException");
        result.Description!.ShouldNotContain("login.microsoftonline.com");
        result.Description!.ShouldNotContain("secret");
    }

    private static FakeHandler CreateHandler(HttpStatusCode statusCode) =>
        new(statusCode);

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(new HttpClient(handler));
        return factory;
    }

    private sealed class FakeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
