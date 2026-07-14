// =============================================================================
// Tests - middleware concurrency through the full Redis-backed stack
// =============================================================================
// N identical parallel requests against a TestServer wired with the REAL
// registration path (AddGranitIdempotency + AddGranitRedisIdempotency, AES key
// configured, Testcontainers Redis) → exactly one 2xx and N−1 409s.
// =============================================================================

using System.Net;
using Granit.Http.Idempotency.Extensions;
using Granit.Http.Idempotency.StackExchangeRedis.Extensions;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IO;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests.Integration;

public sealed class IdempotencyMiddlewareRedisTests(RedisContainerFixture fixture)
    : IClassFixture<RedisContainerFixture>
{
    private const string TestEndpointPath = "/orders";

    private async Task<(HttpClient Client, IHost Host)> BuildTestHostAsync(RequestDelegate endpointHandler)
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("user-42");

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns((Guid?)null);

        IHost host = await new HostBuilder()
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Full production-shaped configuration: TLS relaxed for the test
                // container, AES key present so the fail-closed validator passes.
                ["Http:Idempotency:Redis:Configuration"] = fixture.ConnectionString,
                ["Http:Idempotency:Redis:RequireTls"] = "false",
                ["Http:Idempotency:Redis:InstanceName"] = $"it:mw:{Guid.NewGuid():N}:",
                ["Cache:Encryption:Key"] =
                    Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            }))
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddMetrics();

                    // Real registration path — in-memory default replaced by Redis.
                    services.AddGranitIdempotency();
                    services.AddGranitRedisIdempotency();

                    services.AddScoped<ICurrentUserService>(_ => currentUser);
                    services.AddScoped<ICurrentTenant>(_ => currentTenant);
                });

                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseGranitIdempotency();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints
                            .MapPost(TestEndpointPath, endpointHandler)
                            .WithMetadata(new IdempotentAttribute())
                            .WithName("CreateOrder");
                    });
                });
            })
            .StartAsync();

        return (host.GetTestServer().CreateClient(), host);
    }

    private static HttpRequestMessage BuildRequest() =>
        new(HttpMethod.Post, TestEndpointPath)
        {
            Content = new StringContent(
                """{"amount":100,"currency":"EUR"}""", System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Idempotency-Key", "redis-concurrency-key" } },
        };

    [Fact]
    public async Task GivenParallelIdenticalRequests_ExactlyOne2xx_RestAre409()
    {
        const int Parallelism = 12;

        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerExecutions = 0;

        RequestDelegate blockingHandler = async ctx =>
        {
            Interlocked.Increment(ref handlerExecutions);
            await release.Task.WaitAsync(ctx.RequestAborted);
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"id":1}""");
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(blockingHandler);
        using IHost hostLifetime = host;

        List<Task<HttpResponseMessage>> tasks = [.. Enumerable.Range(0, Parallelism)
            .Select(_ => client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken))];

        // Wait until only the lock winner is still pending (parked on the TCS).
        List<Task<HttpResponseMessage>> pending = [.. tasks];
        while (pending.Count > 1)
        {
            Task<HttpResponseMessage> done = await Task.WhenAny(pending);
            pending.Remove(done);
        }

        release.SetResult();
        HttpResponseMessage[] responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(Parallelism - 1);
        handlerExecutions.ShouldBe(1, "the business handler must execute exactly once");
    }

    [Fact]
    public async Task GivenCompletedRequest_IdenticalRetry_IsReplayedFromRedis()
    {
        int handlerExecutions = 0;
        RequestDelegate handler = async ctx =>
        {
            Interlocked.Increment(ref handlerExecutions);
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            await ctx.Response.WriteAsync("""{"id":1}""");
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(handler);
        using IHost hostLifetime = host;

        HttpResponseMessage first = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);
        HttpResponseMessage second = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.Contains("Idempotent-Replayed").ShouldBeTrue();
        handlerExecutions.ShouldBe(1);
    }
}
