using System.Net;
using Granit.Notifications.SendGrid.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SendGrid.Tests;

public sealed class SendGridHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_SuccessfulResponse_ReturnsHealthy()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/v3/") };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(httpClient);

        SendGridHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CheckHealthAsync_AuthError_ReturnsUnhealthy(HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/v3/") };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(httpClient);

        SendGridHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("auth failed");
    }

    [Fact]
    public async Task CheckHealthAsync_ServerError_ReturnsDegraded()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.sendgrid.com/v3/") };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(httpClient);

        SendGridHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_Exception_ReturnsUnhealthyWithSanitizedMessage()
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SendGrid").Returns(_ => throw new HttpRequestException("connection refused"));

        SendGridHealthCheck sut = new(factory);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("SendGrid unreachable:");
        result.Description!.ShouldNotContain("connection refused");
    }

    private sealed class MockHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
