using System.Net;
using Granit.AI.Ollama.HealthChecks;
using Granit.AI.Ollama.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaHealthCheckTests
{
    private static OllamaHealthCheck CreateHealthCheck(
        HttpMessageHandler handler,
        OllamaOptions? options = null)
    {
        OllamaOptions opts = options ?? new OllamaOptions();
        var httpClient = new HttpClient(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GranitAIOllamaHealthCheck").Returns(httpClient);
        return new OllamaHealthCheck(factory, Microsoft.Extensions.Options.Options.Create(opts));
    }

    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration("ollama", Substitute.For<IHealthCheck>(), null, null),
    };

    [Fact]
    public async Task CheckHealthAsync_SuccessStatusCode_ReturnsHealthy()
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        OllamaHealthCheck healthCheck = CreateHealthCheck(handler);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CheckHealthAsync_NonSuccessStatusCode_ReturnsUnhealthy(HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler(new HttpResponseMessage(statusCode));
        OllamaHealthCheck healthCheck = CreateHealthCheck(handler);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("non-success status");
    }

    [Fact]
    public async Task CheckHealthAsync_TaskCanceledException_ReturnsUnhealthy()
    {
        using var handler = new ThrowingHttpMessageHandler(new TaskCanceledException());
        OllamaHealthCheck healthCheck = CreateHealthCheck(handler);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("timed out");
    }

    [Fact]
    public async Task CheckHealthAsync_HttpRequestException_ReturnsUnhealthy()
    {
        using var handler = new ThrowingHttpMessageHandler(new HttpRequestException());
        OllamaHealthCheck healthCheck = CreateHealthCheck(handler);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("unreachable");
    }

    [Theory]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task CheckHealthAsync_OtherHandledException_ReturnsUnhealthyWithTypeName(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        using var handler = new ThrowingHttpMessageHandler(exception);
        OllamaHealthCheck healthCheck = CreateHealthCheck(handler);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain(exceptionType.Name);
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
