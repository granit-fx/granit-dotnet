using System.Net;
using Granit.AI.Anthropic.HealthChecks;
using Granit.AI.Anthropic.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicHealthCheckTests
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

    private static AnthropicHealthCheck Create(StubHandler handler, AnthropicProviderOptions options) =>
        new(new StubFactory(handler), new TestOptionsMonitor<AnthropicProviderOptions>(options));

    private static AnthropicProviderOptions WithKey() => new()
    {
        ApiKey = "sk-ant-test-123",
        DefaultModel = "claude-sonnet-4-6",
    };

    [Fact]
    public async Task Healthy_when_api_returns_success()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AnthropicHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_when_api_returns_error_status()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        AnthropicHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Healthy_and_no_call_when_host_api_key_absent()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AnthropicProviderOptions noKey = WithKey();
        noKey.ApiKey = string.Empty;
        AnthropicHealthCheck check = Create(handler, noKey);

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Probe_targets_models_endpoint_with_api_key_and_version_headers()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AnthropicHealthCheck check = Create(handler, WithKey());

        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        handler.Calls.ShouldHaveSingleItem();
        handler.Calls[0].RequestUri!.ToString().ShouldBe("https://api.anthropic.com/v1/models");
        handler.Calls[0].Headers.Contains("x-api-key").ShouldBeTrue();
        handler.Calls[0].Headers.Contains("anthropic-version").ShouldBeTrue();
    }

    [Fact]
    public async Task Unhealthy_message_does_not_leak_credential()
    {
        StubHandler handler = new(_ => throw new HttpRequestException("boom sk-ant-test-123"));
        AnthropicHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldNotContain("sk-ant-test-123");
    }
}
