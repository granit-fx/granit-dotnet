using Granit.Http.Resilience.Extensions;
using Granit.Http.Resilience.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Resilience.Tests;

public sealed class AuthTokenPropagationHandlerTests
{
    [Fact]
    public void AddAuthTokenPropagation_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.NotThrow(() => builder.Services
            .AddGranitHttpClient("test-client")
            .AddAuthTokenPropagation());
    }

    [Fact]
    public void AddAuthTokenPropagation_RegistersIHttpContextAccessor()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddGranitHttpClient("test-client")
            .AddAuthTokenPropagation();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IHttpContextAccessor));
    }

    [Fact]
    public async Task SendAsync_PropagatesAuthorizationHeader()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Authorization = "Bearer test-token-123";

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        using AuthTokenPropagationHandler handler = CreateHandler(accessor);
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        handler.InnerHandler = new CapturingHandler(req => capturedRequest = req);

        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("test-token-123");
    }

    [Fact]
    public async Task SendAsync_DoesNotOverrideExistingAuthorizationHeader()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Authorization = "Bearer incoming-token";

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        using AuthTokenPropagationHandler handler = CreateHandler(accessor);
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        handler.InnerHandler = new CapturingHandler(req => capturedRequest = req);

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/test");
        request.Headers.Authorization = new("Bearer", "explicit-token");
        await client.SendAsync(request, TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization!.Parameter.ShouldBe("explicit-token");
    }

    [Fact]
    public async Task SendAsync_NoHttpContext_DoesNotSetHeader()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        using AuthTokenPropagationHandler handler = CreateHandler(accessor);
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        handler.InnerHandler = new CapturingHandler(req => capturedRequest = req);

        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_NoAuthorizationHeader_DoesNotSetHeader()
    {
        DefaultHttpContext httpContext = new();

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        using AuthTokenPropagationHandler handler = CreateHandler(accessor);
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        handler.InnerHandler = new CapturingHandler(req => capturedRequest = req);

        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldBeNull();
    }

    private static AuthTokenPropagationHandler CreateHandler(IHttpContextAccessor accessor) =>
        new(accessor) { InnerHandler = new CapturingHandler(_ => { }) };

    private sealed class CapturingHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
