using Granit.Authentication.DPoP.Middleware;
using Granit.Authentication.DPoP.Options;
using Granit.Authentication.DPoP.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options;

namespace Granit.Authentication.DPoP.Tests.Middleware;

public sealed class DPoPValidationMiddlewareTests
{
    private readonly IDPoPProofValidator _validator = Substitute.For<IDPoPProofValidator>();

    private DPoPValidationMiddleware CreateMiddleware(DPoPValidationOptions options, RequestDelegate next) =>
        new(next, _validator, MsOptions.Options.Create(options), NullLogger<DPoPValidationMiddleware>.Instance);

    [Fact]
    public async Task InvokeAsync_NoDPoPHeader_RequireDPoPFalse_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions { RequireDPoP = false }, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_NoDPoPHeader_RequireDPoPTrue_NotAuthenticated_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        // User.Identity.IsAuthenticated is false by default
        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions { RequireDPoP = true }, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NoDPoPHeader_RequireDPoPTrue_Authenticated_Returns401()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        // Simulate authenticated user
        System.Security.Claims.ClaimsIdentity identity = new("test");
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions { RequireDPoP = true }, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_ValidDPoPProof_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");
        context.Request.Path = "/data";
        context.Request.Headers.Append("DPoP", "valid.proof.jwt");

        _validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(true, "thumb123", null));

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_InvalidDPoPProof_Returns401()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");
        context.Request.Path = "/data";
        context.Request.Headers.Append("DPoP", "invalid.proof.jwt");

        _validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(false, null, "proof_expired"));

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_EmptyDPoPHeader_Returns401()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Headers.Append("DPoP", " ");

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_DPoPSchemeInAuthorization_TriggersValidation()
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");
        context.Request.Path = "/token";
        context.Request.Headers.Authorization = "DPoP eyJhbGciOiJFUzI1NiJ9.proof";

        _validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(true, "thumb123", null));

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        await _validator.Received(1).ValidateAsync(
            "eyJhbGciOiJFUzI1NiJ9.proof",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ExcludedPathPrefix_SkipsValidationAndCallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");
        // Default ExcludedPathPrefixes includes "/connect" — the co-located OIDC token endpoint.
        context.Request.Path = "/connect/token";
        context.Request.Headers.Append("DPoP", "proof.jwt");

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
        // The proof must NOT be validated here — that is the AS pipeline's job.
        await _validator.DidNotReceive().ValidateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ValidProof_ServerNonceSet_AddsResponseHeader()
    {
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");
        context.Request.Path = "/data";
        context.Request.Headers.Append("DPoP", "proof.jwt");

        _validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(true, "thumb", null) { ServerNonce = "server-nonce-123" });

        DPoPValidationMiddleware middleware = CreateMiddleware(new DPoPValidationOptions(), _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers["DPoP-Nonce"].ToString().ShouldBe("server-nonce-123");
    }
}
