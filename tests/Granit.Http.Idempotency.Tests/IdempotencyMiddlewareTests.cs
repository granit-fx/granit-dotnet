// =============================================================================
// Tests - IdempotencyMiddleware
// =============================================================================
// 4 critical scenarios:
//   1. Double-click (InProgress) → HTTP 409 + Retry-After header
//   2. Payload mutation (Completed, wrong hash) → HTTP 422
//   3. Execution timeout → HTTP 503 + DeleteAsync called
//   4. Successful replay → X-Idempotency-Replayed: true (no business logic re-executed)
// =============================================================================

using System.Net;
using System.Net.Http.Headers;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Attributes;
using Granit.Http.Idempotency.Extensions;
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

public sealed class IdempotencyMiddlewareTests
{
    private const string IdempotencyKey = "test-key-abc123";
    private const string TestEndpointPath = "/orders";
    private const string TestRequestBody = """{"amount":100,"currency":"EUR"}""";

    // =========================================================================
    // Test host builder
    // =========================================================================

    private static async Task<(HttpClient Client, IHost Host)> BuildTestHostAsync(
        IIdempotencyStore store,
        Action<IdempotencyOptions>? configureOptions = null,
        RequestDelegate? endpointHandler = null)
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

                    // Options — skip validator registration in tests
                    services.Configure<IdempotencyOptions>(opts =>
                    {
                        opts.ExecutionTimeout = TimeSpan.FromSeconds(25);
                        opts.InProgressTtl = TimeSpan.FromSeconds(30);
                        configureOptions?.Invoke(opts);
                    });

                    // Core idempotency services (without Redis store — mocked below)
                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddTransient<Internal.IdempotencyMiddleware>();

                    // Mocked store (singleton so captured state persists across requests)
                    services.AddSingleton(store);
                    services.AddSingleton<IIdempotencyStore>(store);

                    // Mocked contextual services
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
                            .MapPost(TestEndpointPath, endpointHandler ?? DefaultEndpointHandler)
                            .WithMetadata(new IdempotentAttribute())
                            .WithName("CreateOrder");
                    });
                });
            })
            .StartAsync();

        return (host.GetTestServer().CreateClient(), host);
    }

    private static RequestDelegate DefaultEndpointHandler =>
        async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"id":1,"status":"created"}""");
        };

    private static HttpRequestMessage BuildRequest(string? idempotencyKey = IdempotencyKey) =>
        new(HttpMethod.Post, TestEndpointPath)
        {
            Content = new StringContent(TestRequestBody, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Idempotency-Key", idempotencyKey } },
        };

    // =========================================================================
    // Scenario 1: Double-click → HTTP 409 + Retry-After
    // =========================================================================

    [Fact]
    public async Task GivenKeyInProgress_WhenSameRequestArrives_Returns409WithRetryAfterHeader()
    {
        // Arrange
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        IdempotencyEntry inProgressEntry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = new string('a', 64), // any valid 64-char hex string
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // GetAsync returns InProgress entry on first call
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(inProgressEntry));

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            // Act
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            response.Headers.Contains("Retry-After").ShouldBeTrue();
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("In Progress");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 2: Payload mutation → HTTP 422
    // =========================================================================

    [Fact]
    public async Task GivenCompletedEntry_WhenPayloadDiffers_Returns422()
    {
        // Arrange
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        // Completed entry with a deliberately wrong hash (63 zeros + "1")
        // The middleware will compute the real hash and detect the mismatch.
        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = new string('0', 63) + "1",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 201,
            CompletedAt = DateTimeOffset.UtcNow,
        };

        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(completedEntry));

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            // Act
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("Conflict");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 3: Execution timeout → HTTP 503 + DeleteAsync called
    // =========================================================================

    [Fact]
    public async Task GivenSlowHandler_WhenExecutionTimesOut_Returns503AndReleasesLock()
    {
        // Arrange
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        // No existing entry — lock acquisition succeeds
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        // Endpoint that blocks until the token is cancelled (simulates a hung operation)
        RequestDelegate slowHandler = async ctx =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ctx.RequestAborted);
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(
            store,
            opts =>
            {
                opts.ExecutionTimeout = TimeSpan.FromMilliseconds(150);
                opts.InProgressTtl = TimeSpan.FromSeconds(5); // must be > ExecutionTimeout
            },
            slowHandler);

        try
        {
            // Act
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            // Assert — middleware wrote 503 and released the lock
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            await store.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("Timeout");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 4: No metadata → bypass middleware
    // =========================================================================

    [Fact]
    public async Task GivenEndpointWithoutMetadata_WhenRequestArrives_BypassesMiddleware()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        int handlerCallCount = 0;

        IHost host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.Configure<IdempotencyOptions>(_ => { });
                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddTransient<Internal.IdempotencyMiddleware>();
                    services.AddSingleton(store);
                    services.AddSingleton<IIdempotencyStore>(store);
                    services.AddScoped<ICurrentUserService>(_ => Substitute.For<ICurrentUserService>());
                    services.AddScoped<ICurrentTenant>(_ => Substitute.For<ICurrentTenant>());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseGranitIdempotency();
                    app.UseEndpoints(endpoints =>
                    {
                        // No IdempotentAttribute metadata
                        endpoints.MapPost("/no-idempotency", async ctx =>
                        {
                            handlerCallCount++;
                            ctx.Response.StatusCode = StatusCodes.Status200OK;
                            await ctx.Response.WriteAsync("ok");
                        });
                    });
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

        try
        {
            HttpClient client = host.GetTestServer().CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "/no-idempotency")
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            handlerCallCount.ShouldBe(1);
            // Store should never be called
            await store.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 5: Optional key missing → bypass middleware
    // =========================================================================

    [Fact]
    public async Task GivenOptionalIdempotency_WhenNoKeyHeader_BypassesMiddleware()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        int handlerCallCount = 0;

        IHost host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.Configure<IdempotencyOptions>(_ => { });
                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddTransient<Internal.IdempotencyMiddleware>();
                    services.AddSingleton(store);
                    services.AddSingleton<IIdempotencyStore>(store);
                    services.AddScoped<ICurrentUserService>(_ => Substitute.For<ICurrentUserService>());
                    services.AddScoped<ICurrentTenant>(_ => Substitute.For<ICurrentTenant>());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseGranitIdempotency();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/optional", async ctx =>
                        {
                            handlerCallCount++;
                            ctx.Response.StatusCode = StatusCodes.Status200OK;
                            await ctx.Response.WriteAsync("ok");
                        }).WithMetadata(new IdempotentAttribute { Required = false });
                    });
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

        try
        {
            HttpClient client = host.GetTestServer().CreateClient();
            // No Idempotency-Key header
            var request = new HttpRequestMessage(HttpMethod.Post, "/optional")
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            handlerCallCount.ShouldBe(1);
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 6: Required key missing → HTTP 422
    // =========================================================================

    [Fact]
    public async Task GivenRequiredIdempotency_WhenNoKeyHeader_Returns422()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            // No Idempotency-Key header
            var request = new HttpRequestMessage(HttpMethod.Post, TestEndpointPath)
            {
                Content = new StringContent(TestRequestBody, System.Text.Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("Missing Idempotency-Key");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 7: Multipart request → HTTP 422
    // =========================================================================

    [Fact]
    public async Task GivenMultipartRequest_Returns422()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            using var multipartContent = new MultipartFormDataContent();
            multipartContent.Add(new StringContent("test"), "field");

            var request = new HttpRequestMessage(HttpMethod.Post, TestEndpointPath)
            {
                Content = multipartContent,
                Headers = { { "Idempotency-Key", IdempotencyKey } },
            };

            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("Unsupported Content-Type");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 8: Race condition — TryAcquire fails, re-read returns null
    // =========================================================================

    [Fact]
    public async Task GivenRaceCondition_WhenAcquireFailsAndReReadNull_ProceedsWithoutLock()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        // First GetAsync: null (no entry)
        // TryAcquire: fails
        // Second GetAsync (re-read): null (vanished)
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        store.SetCompletedAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            // Handler still executes despite acquire failure
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 9: Race condition — TryAcquire fails, re-read returns InProgress
    // =========================================================================

    [Fact]
    public async Task GivenRaceCondition_WhenAcquireFailsAndReReadInProgress_Returns409()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        var inProgressEntry = new IdempotencyEntry
        {
            State = IdempotencyState.InProgress,
            PayloadHash = new string('a', 64),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // First GetAsync: null (no entry) — passes first check
        // TryAcquire: fails (another pod won the race)
        // Second GetAsync (re-read): returns InProgress
        int getCallCount = 0;
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(_ =>
             {
                 getCallCount++;
                 return Task.FromResult<IdempotencyEntry?>(getCallCount == 1 ? null : inProgressEntry);
             });

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(false));

        (HttpClient client, IHost host) = await BuildTestHostAsync(store);

        try
        {
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 10: Handler throws exception → lock released
    // =========================================================================

    [Fact]
    public async Task GivenHandlerThrows_WhenExceptionOccurs_ReleasesLockAndPropagates()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        RequestDelegate throwingHandler = _ => throw new InvalidOperationException("boom");

        (HttpClient client, IHost host) = await BuildTestHostAsync(store, endpointHandler: throwingHandler);

        try
        {
            // TestServer propagates unhandled exceptions to the caller
            await Should.ThrowAsync<InvalidOperationException>(
                () => client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken));

            // The middleware should have released the lock before re-throwing
            await store.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 11: Non-cacheable status code → lock deleted
    // =========================================================================

    [Fact]
    public async Task GivenNonCacheableStatusCode_WhenHandlerReturns401_DeletesEntry()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        RequestDelegate unauthorizedHandler = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(store, endpointHandler: unauthorizedHandler);

        try
        {
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            await store.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await store.DidNotReceive().SetCompletedAsync(
                Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 12: First request success → stores with correct TTL
    // =========================================================================

    [Fact]
    public async Task GivenFirstRequest_WhenHandlerSucceeds_StoresCompletedEntryWithHeaders()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        IdempotencyEntry? captured = null;

        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.SetCompletedAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyEntry>(e => captured = e),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        RequestDelegate handler = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["X-Custom-Header"] = "value";
            await ctx.Response.WriteAsync("""{"result":"ok"}""");
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(store, endpointHandler: handler);

        try
        {
            HttpResponseMessage response = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            captured.ShouldNotBeNull();
            captured.State.ShouldBe(IdempotencyState.Completed);
            captured.StatusCode.ShouldBe(200);
            captured.ResponseBody.ShouldNotBeNull();
            captured.ResponseBody.Length.ShouldBeGreaterThan(0);
            captured.ResponseHeaders.ShouldNotBeNull();
            captured.CompletedAt.ShouldNotBeNull();
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 13: Replay with response body and headers
    // =========================================================================

    [Fact]
    public async Task GivenCompletedEntryWithBodyAndHeaders_WhenReplayed_RestoresFullResponse()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        // We need to compute the real payload hash, so first let the middleware do the first request
        IdempotencyEntry? captured = null;

        int callCount = 0;
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(_ => Task.FromResult<IdempotencyEntry?>(callCount++ == 0 ? null : captured));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.SetCompletedAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyEntry>(e => captured = e),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        RequestDelegate handler = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["X-Request-Id"] = "req-123";
            await ctx.Response.WriteAsync("""{"id":42}""");
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(store, endpointHandler: handler);

        try
        {
            // First request
            HttpResponseMessage first = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);
            first.StatusCode.ShouldBe(HttpStatusCode.Created);
            captured.ShouldNotBeNull();

            // Second request (replay)
            HttpResponseMessage second = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            second.StatusCode.ShouldBe(HttpStatusCode.Created);
            second.Headers.Contains("X-Idempotency-Replayed").ShouldBeTrue();
            string replayBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            replayBody.ShouldContain("42");
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 14: Custom CompletedTtlSeconds from attribute
    // =========================================================================

    [Fact]
    public async Task GivenCustomCompletedTtl_WhenStored_UsesAttributeTtl()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        TimeSpan? capturedTtl = null;

        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IdempotencyEntry?>(null));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.SetCompletedAsync(
                Arg.Any<string>(),
                Arg.Any<IdempotencyEntry>(),
                Arg.Do<TimeSpan>(t => capturedTtl = t),
                Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

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
                    services.Configure<IdempotencyOptions>(_ => { });
                    services.AddSingleton<TimeProvider>(TimeProvider.System);
                    services.AddSingleton<RecyclableMemoryStreamManager>();
                    services.AddTransient<Internal.IdempotencyMiddleware>();
                    services.AddSingleton(store);
                    services.AddSingleton<IIdempotencyStore>(store);
                    services.AddScoped<ICurrentUserService>(_ => currentUser);
                    services.AddScoped<ICurrentTenant>(_ => currentTenant);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseGranitIdempotency();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/custom-ttl", async ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status200OK;
                            await ctx.Response.WriteAsync("ok");
                        }).WithMetadata(new IdempotentAttribute { CompletedTtlSeconds = 3600 });
                    });
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

        try
        {
            HttpClient client = host.GetTestServer().CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "/custom-ttl")
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
                Headers = { { "Idempotency-Key", "custom-key" } },
            };

            await client.SendAsync(request, TestContext.Current.CancellationToken);

            capturedTtl.ShouldNotBeNull();
            capturedTtl.Value.ShouldBe(TimeSpan.FromSeconds(3600));
        }
        finally
        {
            host.Dispose();
        }
    }

    // =========================================================================
    // Scenario 15: Successful replay → X-Idempotency-Replayed (no re-execution)
    // =========================================================================

    [Fact]
    public async Task GivenCompletedFirstRequest_WhenRetriedWithSameKey_ReplaysWithoutExecutingBusinessLogic()
    {
        // Arrange
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();

        int handlerCallCount = 0;
        IdempotencyEntry? capturedCompleted = null;

        // First call: no entry; lock acquired; handler executes; SetCompleted captures the entry
        store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(callInfo => Task.FromResult<IdempotencyEntry?>(capturedCompleted));

        store.TryAcquireAsync(Arg.Any<string>(), Arg.Any<IdempotencyEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(true));

        store.SetCompletedAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyEntry>(e => capturedCompleted = e),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        RequestDelegate countingHandler = async ctx =>
        {
            handlerCallCount++;
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"id":99}""");
        };

        (HttpClient client, IHost host) = await BuildTestHostAsync(store, endpointHandler: countingHandler);

        try
        {
            // Act — first request (executes handler, stores completed entry)
            HttpResponseMessage first = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);
            first.StatusCode.ShouldBe(HttpStatusCode.Created);

            // capturedCompleted is now set by the SetCompletedAsync callback;
            // second request's GetAsync will return it.
            capturedCompleted.ShouldNotBeNull("SetCompletedAsync must have been called");

            // Act — second request (replay, no handler re-execution)
            HttpResponseMessage second = await client.SendAsync(BuildRequest(), TestContext.Current.CancellationToken);

            // Assert
            second.StatusCode.ShouldBe(HttpStatusCode.Created);
            second.Headers.Contains("X-Idempotency-Replayed").ShouldBeTrue();
            second.Headers.GetValues("X-Idempotency-Replayed").ShouldContain("true");

            handlerCallCount.ShouldBe(1, "business logic must not be re-executed on replay");
        }
        finally
        {
            host.Dispose();
        }
    }
}
