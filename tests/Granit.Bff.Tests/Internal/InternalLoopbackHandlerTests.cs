using System.Net;
using Granit.Bff.Internal;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.Internal;

public sealed class InternalLoopbackHandlerTests
{
    private const string Authority = "https://localhost:5001";

    private readonly BffLoopbackPipelineCapture _pipelineCapture = new();
    private readonly IServer _server = Substitute.For<IServer>();
    private readonly IServiceScopeFactory _scopeFactory;

    public InternalLoopbackHandlerTests()
    {
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    private InternalLoopbackHandler CreateHandler(
        string authority = Authority,
        IReadOnlyList<string>? serverAddresses = null)
    {
        ServerAddressesFeature addressesFeature = new();
        foreach (string address in serverAddresses ?? [authority])
        {
            addressesFeature.Addresses.Add(address);
        }

        FeatureCollection features = new();
        features.Set<IServerAddressesFeature>(addressesFeature);
        _server.Features.Returns(features);

        Microsoft.Extensions.Options.IOptions<GranitBffOptions> options = Microsoft.Extensions.Options.Options.Create(new GranitBffOptions
        {
            Authority = new Uri(authority),
        });

        InternalLoopbackHandler handler = new(
            _pipelineCapture,
            _server,
            options,
            _scopeFactory,
            NullLogger<InternalLoopbackHandler>.Instance)
        {
            InnerHandler = new StubInnerHandler(),
        };

        return handler;
    }

    // --- Loopback detection ---

    [Fact]
    public async Task SendAsync_DelegatesToInnerHandler_WhenPipelineNotCaptured()
    {
        using InternalLoopbackHandler handler = CreateHandler();
        // pipelineCapture.Pipeline is null (no IStartupFilter ran)
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent); // StubInnerHandler returns 204
    }

    [Fact]
    public async Task SendAsync_DelegatesToInnerHandler_WhenAuthorityDoesNotMatchServer()
    {
        using InternalLoopbackHandler handler = CreateHandler(
            authority: "https://auth.external.com",
            serverAddresses: ["https://localhost:5001"]);
        CapturePipeline(context => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, "https://auth.external.com/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent); // StubInnerHandler, not pipeline
    }

    [Fact]
    public async Task SendAsync_InvokesPipeline_WhenAuthorityMatchesServerAddress()
    {
        using InternalLoopbackHandler handler = CreateHandler();
        CapturePipeline(context =>
        {
            context.Response.StatusCode = 200;
            return context.Response.WriteAsync("""{"access_token":"in-memory"}""");
        });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("in-memory");
    }

    [Fact]
    public async Task SendAsync_InvokesPipeline_WhenServerUsesWildcardHost()
    {
        using InternalLoopbackHandler handler = CreateHandler(
            authority: "https://localhost:5001",
            serverAddresses: ["https://+:5001"]);
        CapturePipeline(context => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("https://0.0.0.0:5001")]
    [InlineData("https://[::]:5001")]
    [InlineData("https://*:5001")]
    public async Task SendAsync_InvokesPipeline_ForAllWildcardFormats(string serverAddress)
    {
        using InternalLoopbackHandler handler = CreateHandler(
            authority: Authority,
            serverAddresses: [serverAddress]);
        CapturePipeline(context => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_DelegatesToInnerHandler_WhenPortMismatch()
    {
        using InternalLoopbackHandler handler = CreateHandler(
            authority: "https://localhost:5001",
            serverAddresses: ["https://localhost:5002"]);
        CapturePipeline(context => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, "https://localhost:5001/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent); // Inner handler
    }

    [Fact]
    public async Task SendAsync_DelegatesToInnerHandler_WhenSchemeMismatch()
    {
        using InternalLoopbackHandler handler = CreateHandler(
            authority: "https://localhost:5001",
            serverAddresses: ["http://localhost:5001"]);
        CapturePipeline(context => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, "https://localhost:5001/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // --- Request/response mapping ---

    [Fact]
    public async Task SendAsync_MapsRequestHeadersToPipeline()
    {
        string? capturedAuth = null;
        using InternalLoopbackHandler handler = CreateHandler();
        CapturePipeline(context =>
        {
            capturedAuth = context.Request.Headers.Authorization;
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "Y2xpZW50OnNlY3JldA==");
        using HttpResponseMessage _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        capturedAuth.ShouldNotBeNull();
        capturedAuth.ShouldContain("Basic");
    }

    [Fact]
    public async Task SendAsync_MapsFormBodyToPipeline()
    {
        string? capturedBody = null;
        using InternalLoopbackHandler handler = CreateHandler();
        CapturePipeline(async context =>
        {
            using StreamReader reader = new(context.Request.Body);
            capturedBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            context.Response.StatusCode = 200;
        });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = "test-code",
            }),
        };
        using HttpResponseMessage _ = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("grant_type=authorization_code");
        capturedBody.ShouldContain("code=test-code");
    }

    [Fact]
    public async Task SendAsync_MapsResponseHeaders()
    {
        using InternalLoopbackHandler handler = CreateHandler();
        CapturePipeline(context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Headers["DPoP-Nonce"] = "server-nonce-42";
            return Task.CompletedTask;
        });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? values).ShouldBeTrue();
        values!.ShouldContain("server-nonce-42");
    }

    [Fact]
    public async Task SendAsync_PropagatesErrorStatusFromPipeline()
    {
        using InternalLoopbackHandler handler = CreateHandler();
        CapturePipeline(context =>
        {
            context.Response.StatusCode = 400;
            return context.Response.WriteAsync("""{"error":"invalid_grant"}""");
        });
        using HttpMessageInvoker invoker = new(handler);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{Authority}/connect/token");
        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("invalid_grant");
    }

    // --- Helpers ---

    private void CapturePipeline(RequestDelegate pipeline) =>
        _pipelineCapture.Pipeline = pipeline;

    /// <summary>
    /// Minimal handler that returns 204 No Content — used to verify that
    /// the loopback handler correctly delegates to the inner handler
    /// when the request should not be short-circuited.
    /// </summary>
    private sealed class StubInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
}
