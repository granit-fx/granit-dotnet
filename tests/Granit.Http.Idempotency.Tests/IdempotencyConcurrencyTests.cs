// =============================================================================
// Tests - end-to-end concurrency through the middleware (real in-memory store)
// =============================================================================
// N identical parallel requests → exactly one 2xx (the lock winner executes the
// handler) and N−1 409s (everyone else hits InProgress). No mocks on the store:
// this exercises the real atomic create-if-absent transition under contention.
// =============================================================================

using System.Net;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Extensions;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IO;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyConcurrencyTests
{
    private const string TestEndpointPath = "/orders";

    private static async Task<(HttpClient Client, IHost Host)> BuildTestHostAsync(RequestDelegate endpointHandler)
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("user-42");

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns((Guid?)null);

        IHost host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();

                    services.Configure<IdempotencyOptions>(opts =>
                    {
                        opts.ExecutionTimeout = TimeSpan.FromSeconds(25);
                        opts.InProgressTtl = TimeSpan.FromSeconds(30);
                    });

                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddMetrics();
                    services.AddSingleton<Diagnostics.IdempotencyMetrics>();
                    services.AddTransient<IdempotencyMiddleware>();

                    // REAL store — the whole point of this test
                    services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

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
            Headers = { { "Idempotency-Key", "concurrency-key-1" } },
        };

    [Fact]
    public async Task GivenParallelIdenticalRequests_ExactlyOne2xx_RestAre409()
    {
        const int Parallelism = 12;

        // The single lock winner blocks inside the handler until every loser has
        // received its 409 — deterministic contention, no sleeps.
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

        // Wait until only the winner is still pending (it is parked on the TCS);
        // all other requests must have completed with 409 by then.
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

        foreach (HttpResponseMessage conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            conflict.Headers.RetryAfter.ShouldNotBeNull("409s must carry Retry-After");
        }
    }

    [Fact]
    public async Task GivenSequentialIdenticalRequests_SecondIsReplayed()
    {
        // Sanity companion: after the winner completes, an identical retry replays
        // the stored response instead of conflicting or re-executing.
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
