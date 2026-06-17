using Granit.OpenIddict.Server.Handlers;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using Shouldly;
using Xunit;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace Granit.OpenIddict.Server.Tests.Handlers;

public sealed class DPoPValidationTokenExtractionHandlerTests
{
    private const string Token = "header.payload.signature";

    [Fact]
    public async Task DPoPScheme_PopulatesAccessToken()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: $"DPoP {Token}");

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBe(Token);
    }

    [Fact]
    public async Task DPoPScheme_CaseInsensitivePrefix_PopulatesAccessToken()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: $"dpop {Token}");

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBe(Token);
    }

    [Fact]
    public async Task BearerAlreadyExtracted_LeavesAccessTokenUntouched()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: $"DPoP {Token}");
        context.AccessToken = "already-extracted-by-bearer-handler";

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBe("already-extracted-by-bearer-handler");
    }

    [Fact]
    public async Task BearerScheme_IsIgnored()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: $"Bearer {Token}");

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task NoAuthorizationHeader_LeavesAccessTokenNull()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: null);

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task NoHttpRequest_LeavesAccessTokenNull()
    {
        DPoPValidationTokenExtractionHandler handler = new();
        ProcessAuthenticationContext context = BuildContext(authorization: null, attachHttpRequest: false);

        await handler.HandleAsync(context);

        context.AccessToken.ShouldBeNullOrEmpty();
    }

    private static ProcessAuthenticationContext BuildContext(
        string? authorization, bool attachHttpRequest = true)
    {
        OpenIddictValidationTransaction transaction = new()
        {
            Request = new OpenIddictRequest(),
            Response = new OpenIddictResponse(),
        };

        if (attachHttpRequest)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("api.example.com");
            httpContext.Request.Path = "/resource";
            if (authorization is not null)
            {
                httpContext.Request.Headers["Authorization"] = authorization;
            }

            // OpenIddictValidationAspNetCoreHelpers.GetHttpRequest() reads a
            // WeakReference<HttpRequest> keyed by the HttpRequest full type name.
            transaction.Properties[typeof(HttpRequest).FullName!] =
                new WeakReference<HttpRequest>(httpContext.Request);
        }

        return new ProcessAuthenticationContext(transaction);
    }
}
