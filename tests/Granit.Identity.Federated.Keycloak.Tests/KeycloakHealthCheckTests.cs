using System.Net;
using Granit.Identity.Federated.Keycloak.HealthChecks;
using Granit.Identity.Federated.Keycloak.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakHealthCheckTests
{
    private readonly KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.example.com",
        Realm = "test",
        ClientId = "svc",
        ClientSecret = "secret",
    };

    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_SuccessfulToken_ReturnsHealthy()
    {
        // Arrange
        HttpMessageHandler handler = CreateHandler(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        KeycloakHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

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
        HttpMessageHandler handler = CreateHandler(HttpStatusCode.Unauthorized);
        IHttpClientFactory factory = CreateFactory(handler);
        KeycloakHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

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
        HttpMessageHandler handler = CreateHandler(HttpStatusCode.InternalServerError);
        IHttpClientFactory factory = CreateFactory(handler);
        KeycloakHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

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
        factory.CreateClient("KeycloakAdmin")
            .Returns(_ => throw new HttpRequestException("Connection refused to https://keycloak:8443 with secret=xyz"));

        KeycloakHealthCheck sut = new(factory, Microsoft.Extensions.Options.Options.Create(_options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Keycloak unreachable: HttpRequestException");
        result.Description!.ShouldNotContain("keycloak:8443");
        result.Description!.ShouldNotContain("secret");
    }

    private static FakeHandler CreateHandler(HttpStatusCode statusCode) =>
        new(statusCode);

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("KeycloakAdmin").Returns(new HttpClient(handler));
        return factory;
    }

    private sealed class FakeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
