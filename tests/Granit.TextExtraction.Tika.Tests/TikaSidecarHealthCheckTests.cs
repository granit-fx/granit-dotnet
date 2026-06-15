using System.Net;
using Granit.TextExtraction.Tika.HealthChecks;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Tika.Tests;

public sealed class TikaSidecarHealthCheckTests
{
    private static readonly Uri TikaUri = new("https://tika.test/");

    private static TikaSidecarHealthCheck Create(StubHttpMessageHandler stub)
    {
        ServiceCollection services = new();
        services.AddHttpClient(TikaSidecarTextExtractor.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();

        TikaSidecarOptions opts = new() { Uri = TikaUri, AllowedHosts = ["tika.test"] };
        return new TikaSidecarHealthCheck(factory, MEOptions.Create(opts));
    }

    [Fact]
    public async Task Healthy_when_sidecar_returns_success()
    {
        TikaSidecarHealthCheck check = Create(
            StubHttpMessageHandler.RespondWith(HttpStatusCode.OK, "Apache Tika 2.9.1"));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Unhealthy_when_sidecar_returns_error_status()
    {
        TikaSidecarHealthCheck check = Create(
            StubHttpMessageHandler.RespondWith(HttpStatusCode.ServiceUnavailable, string.Empty));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Unhealthy_message_does_not_leak_sidecar_host()
    {
        TikaSidecarHealthCheck check = Create(
            StubHttpMessageHandler.Throws(new HttpRequestException("connection refused to tika.test")));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldNotContain("tika.test");
    }

    [Fact]
    public async Task Probes_the_version_endpoint()
    {
        var stub =
            StubHttpMessageHandler.RespondWith(HttpStatusCode.OK, "Apache Tika 2.9.1");
        TikaSidecarHealthCheck check = Create(stub);

        await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        stub.Calls.ShouldHaveSingleItem();
        stub.Calls[0].RequestUri!.AbsolutePath.ShouldBe("/version");
        stub.Calls[0].Method.ShouldBe(HttpMethod.Get);
    }
}
