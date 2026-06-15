using System.Net;
using Granit.AI.AzureOpenAI.HealthChecks;
using Granit.AI.AzureOpenAI.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class AzureOpenAIHealthCheckTests
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

    private static AzureOpenAIHealthCheck Create(StubHandler handler, AzureOpenAIProviderOptions options) =>
        new(new StubFactory(handler), new TestOptionsMonitor<AzureOpenAIProviderOptions>(options));

    private static AzureOpenAIProviderOptions WithKey() => new()
    {
        Endpoint = "https://test.openai.azure.com",
        ApiKey = "azure-test-key",
        DefaultDeployment = "gpt-4o",
        DefaultEmbeddingDeployment = "text-embedding-3-small",
    };

    [Fact]
    public async Task Healthy_when_api_returns_success()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AzureOpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_when_api_returns_error_status()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        AzureOpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Healthy_and_no_call_when_no_key_and_managed_identity_disabled()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AzureOpenAIProviderOptions noCred = WithKey();
        noCred.ApiKey = string.Empty;
        noCred.AllowManagedIdentityFallback = false;
        AzureOpenAIHealthCheck check = Create(handler, noCred);

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Probe_targets_models_endpoint_with_api_key_header()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AzureOpenAIHealthCheck check = Create(handler, WithKey());

        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        handler.Calls.ShouldHaveSingleItem();
        handler.Calls[0].RequestUri!.ToString()
            .ShouldBe("https://test.openai.azure.com/openai/models?api-version=2024-10-21");
        handler.Calls[0].Headers.Contains("api-key").ShouldBeTrue();
    }

    [Fact]
    public async Task Unhealthy_message_does_not_leak_credential()
    {
        StubHandler handler = new(_ => throw new HttpRequestException("boom azure-test-key"));
        AzureOpenAIHealthCheck check = Create(handler, WithKey());

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldNotContain("azure-test-key");
    }
}
