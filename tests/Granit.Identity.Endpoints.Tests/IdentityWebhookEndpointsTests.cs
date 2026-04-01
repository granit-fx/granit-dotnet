using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Endpoints.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for the identity webhook endpoint (signature validation, event routing).
/// </summary>
public sealed class IdentityWebhookEndpointsTests : IAsyncDisposable
{
    private const string WebhookSecret = "test-webhook-secret-key-32-chars!";
    private const string WebhookUrl = "/identity/webhook";

    private readonly IUserLookupService _lookupService = Substitute.For<IUserLookupService>();
    private readonly IUserCacheStats _cacheStats = Substitute.For<IUserCacheStats>();
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public IdentityWebhookEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read,
                policy => policy.RequireRole("granit-identity-admin"))
            .AddPolicy(IdentityPermissions.Users.Sync,
                policy => policy.RequireRole("granit-identity-admin"))
            .AddPolicy(IdentityPermissions.Users.Delete,
                policy => policy.RequireRole("granit-identity-admin"));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_lookupService);
        builder.Services.AddSingleton(_cacheStats);

        // Configure webhook with a secret
        builder.Services.Configure<IdentityWebhookOptions>(o =>
        {
            o.Secret = WebhookSecret;
        });

        _app = builder.Build();
        _app.MapGranitIdentityUserCache();
        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Webhook_user_updated_calls_RefreshByIdAsync()
    {
        var payload = new { eventType = "user_updated", userId = "user-1" };
        using HttpRequestMessage request = CreateSignedRequest(payload);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _lookupService.Received(1).RefreshByIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Webhook_user_created_calls_RefreshByIdAsync()
    {
        var payload = new { eventType = "user_created", userId = "user-2" };
        using HttpRequestMessage request = CreateSignedRequest(payload);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _lookupService.Received(1).RefreshByIdAsync("user-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Webhook_user_deleted_calls_DeleteByIdAsync()
    {
        var payload = new { eventType = "user_deleted", userId = "user-1" };
        using HttpRequestMessage request = CreateSignedRequest(payload);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _lookupService.Received(1).DeleteByIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Webhook_invalid_signature_returns_401()
    {
        var payload = new { eventType = "user_updated", userId = "user-1" };
        string json = JsonSerializer.Serialize(payload);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", "invalid-signature");

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_missing_signature_returns_401()
    {
        var payload = new { eventType = "user_updated", userId = "user-1" };
        string json = JsonSerializer.Serialize(payload);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_invalid_json_returns_400()
    {
        string invalidJson = "{ not valid json }}}";
        byte[] body = Encoding.UTF8.GetBytes(invalidJson);
        string signature = ComputeSignature(body);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(invalidJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", signature);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_unknown_event_type_returns_400_without_reflecting_input()
    {
        var payload = new { eventType = "unknown_event", userId = "user-1" };
        using HttpRequestMessage request = CreateSignedRequest(payload);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // VULN-204: verify user-controlled data is NOT reflected in the response
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("unknown_event");
        body.ShouldContain("Unsupported event type.");
    }

    [Fact]
    public async Task Webhook_missing_userId_returns_400()
    {
        var payload = new { eventType = "user_updated", userId = "" };
        using HttpRequestMessage request = CreateSignedRequest(payload);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ──── VULN-101: Oversized payload ────

    [Fact]
    public async Task Webhook_oversized_payload_returns_413()
    {
        // 65 KB > 64 KB limit
        string largeJson = new('x', 65 * 1024);
        byte[] body = Encoding.UTF8.GetBytes(largeJson);
        string signature = ComputeSignature(body);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(largeJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", signature);

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        // 413 Payload Too Large (returned as ProblemDetails which maps to the status code)
        ((int)response.StatusCode).ShouldBe(413);
    }

    private static HttpRequestMessage CreateSignedRequest(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);
        string signature = ComputeSignature(body);

        HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", signature);

        return request;
    }

    private static string ComputeSignature(byte[] body)
    {
        byte[] key = Encoding.UTF8.GetBytes(WebhookSecret);
        byte[] hash = HMACSHA256.HashData(key, body);
        return Convert.ToHexStringLower(hash);
    }
}

/// <summary>
/// Tests for the fail-closed behavior when the webhook secret is not configured (VULN-001).
/// Uses a separate <see cref="WebApplication"/> without a configured secret.
/// </summary>
public sealed class IdentityWebhookEndpointsNoSecretTests : IAsyncDisposable
{
    private const string WebhookUrl = "/identity/webhook";

    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public IdentityWebhookEndpointsNoSecretTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read,
                policy => policy.RequireRole("granit-identity-admin"))
            .AddPolicy(IdentityPermissions.Users.Sync,
                policy => policy.RequireRole("granit-identity-admin"))
            .AddPolicy(IdentityPermissions.Users.Delete,
                policy => policy.RequireRole("granit-identity-admin"));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(Substitute.For<IUserLookupService>());
        builder.Services.AddSingleton(Substitute.For<IUserCacheStats>());

        // No secret configured — IdentityWebhookOptions.Secret remains ""

        _app = builder.Build();
        _app.MapGranitIdentityUserCache();
        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Webhook_rejects_all_requests_when_secret_not_configured()
    {
        var payload = new { eventType = "user_updated", userId = "user-1" };
        string json = JsonSerializer.Serialize(payload);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_rejects_even_with_forged_signature_when_no_secret()
    {
        var payload = new { eventType = "user_updated", userId = "user-1" };
        string json = JsonSerializer.Serialize(payload);

        using HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", "some-forged-signature");

        using HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
