using System.Net;
using Granit.Notifications.Scaleway.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Scaleway.Tests;

public sealed class ScalewayEmailHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_SuccessfulResponse_ReturnsHealthy()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, string.Empty);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.scaleway.com/transactional-email/v1alpha1/regions/fr-par/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(httpClient);

        ScalewayEmailHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CheckHealthAsync_AuthError_ReturnsUnhealthy(HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler(statusCode, string.Empty);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.scaleway.com/transactional-email/v1alpha1/regions/fr-par/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(httpClient);

        ScalewayEmailHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("auth failed");
    }

    [Fact]
    public async Task CheckHealthAsync_ServerError_ReturnsDegraded()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.scaleway.com/transactional-email/v1alpha1/regions/fr-par/"),
        };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(httpClient);

        ScalewayEmailHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_Exception_ReturnsUnhealthyWithSanitizedMessage()
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Scaleway").Returns(_ => throw new HttpRequestException("connection refused"));

        ScalewayEmailHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("Scaleway unreachable:");
        result.Description!.ShouldNotContain("connection refused");
    }
}
