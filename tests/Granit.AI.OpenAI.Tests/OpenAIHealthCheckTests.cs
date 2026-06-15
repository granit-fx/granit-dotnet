using System.Net;
using Granit.AI.OpenAI.HealthChecks;
using Granit.AI.OpenAI.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAIHealthCheckTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static OpenAIHealthCheck Create(StubHandler handler, OpenAIProviderOptions options) =>
        new(new StubFactory(handler), new TestOptionsMonitor<OpenAIProviderOptions>(options));

    private static OpenAIProviderOptions WithKey() => new()
    {
        ApiKey = "sk-test-key-123",
        DefaultModel = "gpt-4o",
        DefaultEmbeddingModel = "text-embedding-3-small",
    };

    [Fact]
    public async Task Healthy_when_api_returns_success()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        OpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_when_api_returns_error_status()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        OpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Healthy_and_no_call_when_host_api_key_absent()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        OpenAIProviderOptions noKey = WithKey();
        noKey.ApiKey = string.Empty;
        OpenAIHealthCheck check = Create(handler, noKey);

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Probe_targets_models_endpoint_with_bearer_auth()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        OpenAIHealthCheck check = Create(handler, WithKey());

        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        handler.Calls.ShouldHaveSingleItem();
        handler.Calls[0].RequestUri!.ToString().ShouldBe("https://api.openai.com/v1/models");
        handler.Calls[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
    }

    [Fact]
    public async Task Unhealthy_message_does_not_leak_credential()
    {
        StubHandler handler = new(_ => throw new HttpRequestException("boom sk-test-key-123"));
        OpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldNotContain("sk-test-key-123");
    }
}
