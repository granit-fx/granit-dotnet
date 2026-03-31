using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Granit.Bff;
using Granit.Bff.Endpoints.Endpoints;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Endpoints.Tests.Integration;

public sealed class BffEndpointsIntegrationTests : IAsyncLifetime
{
    private BffEndpointsTestServer _server = null!;

    /// <summary>
    /// A fake session ID used to simulate a browser with an active BFF session cookie.
    /// </summary>
    private const string TestSessionId = "test-session-id-1234567890abcdef";

    public async ValueTask InitializeAsync() =>
        _server = await BffEndpointsTestServer.CreateAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() =>
        await _server.DisposeAsync().ConfigureAwait(false);

    // ──── Helpers ────

    /// <summary>
    /// Creates an <see cref="HttpRequestMessage"/> with the session cookie attached.
    /// </summary>
    private static HttpRequestMessage CreateRequestWithSession(
        HttpMethod method, string url, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, url) { Content = content };
        request.Headers.Add("Cookie",
            $"{BffEndpointsTestServer.TestSessionCookieName}={TestSessionId}");
        return request;
    }

    /// <summary>
    /// Sends a request through the authenticated client with the session cookie attached.
    /// </summary>
    private async Task<HttpResponseMessage> SendAuthenticatedWithSessionAsync(
        HttpMethod method, string url, HttpContent? content = null)
    {
        using HttpRequestMessage request = CreateRequestWithSession(method, url, content);
        request.Headers.Add(TestAuthHandler.RolesHeader, "authenticated");
        return await _server.AuthenticatedClient.SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request through the anonymous client (no auth header) with the session cookie.
    /// </summary>
    private async Task<HttpResponseMessage> SendAnonymousWithSessionAsync(
        HttpMethod method, string url, HttpContent? content = null)
    {
        using HttpRequestMessage request = CreateRequestWithSession(method, url, content);
        return await _server.AnonymousClient.SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a minimal unsigned JWT with the specified claims for testing.
    /// </summary>
    private static string BuildTestJwt(object payload)
    {
        string header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        string payloadJson = JsonSerializer.Serialize(payload);
        string body = Base64UrlEncode(payloadJson);
        return $"{header}.{body}.";
    }

    private static string Base64UrlEncode(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // -------------------------------------------------------------------------
    // CSRF endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostCsrfToken_WithSessionCookie_Returns200WithToken()
    {
        _server.CsrfGenerator.Generate(TestSessionId).Returns("csrf-token-value");

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Post, "/app/bff/csrf-token");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        result.GetProperty("csrfToken").GetString().ShouldBe("csrf-token-value");
    }

    [Fact]
    public async Task PostCsrfToken_WithoutSessionCookie_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/app/bff/csrf-token", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // User endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUser_WithSessionAndValidTokens_Returns200WithUserInfo()
    {
        string idToken = BuildTestJwt(new
        {
            sub = "user-42",
            name = "Jane Doe",
            email = "jane@example.com",
            roles = new[] { "admin", "user" },
            tenant_id = "tenant-1",
        });

        BffTokenSet tokens = new(
            "access-token",
            "refresh-token",
            idToken,
            BffEndpointsTestServer.FixedNow.AddHours(1));

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(tokens);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Get, "/app/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        result.GetProperty("authenticated").GetBoolean().ShouldBeTrue();
        result.GetProperty("sub").GetString().ShouldBe("user-42");
        result.GetProperty("name").GetString().ShouldBe("Jane Doe");
        result.GetProperty("email").GetString().ShouldBe("jane@example.com");
    }

    [Fact]
    public async Task GetUser_WithoutSessionCookie_Returns200WithAuthenticatedFalse()
    {
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/app/bff/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        result.GetProperty("authenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetUser_SessionCookieButNoTokensInStore_Returns200WithAuthenticatedFalse()
    {
        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns((BffTokenSet?)null);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Get, "/app/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        result.GetProperty("authenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetUser_TokensWithoutIdToken_Returns200WithAuthenticatedFalse()
    {
        BffTokenSet tokens = new(
            "access-token", null, null,
            BffEndpointsTestServer.FixedNow.AddHours(1));

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(tokens);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Get, "/app/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        result.GetProperty("authenticated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetUser_ValidSession_ResponseIncludesNoCacheHeader()
    {
        string idToken = BuildTestJwt(new
        {
            sub = "user-99",
            name = "Cache Test",
            email = "cache@example.com",
        });

        BffTokenSet tokens = new(
            "access-token", null, idToken,
            BffEndpointsTestServer.FixedNow.AddHours(1));

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(tokens);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Get, "/app/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Session endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSessions_WithSessionCookie_Returns200WithSessionList()
    {
        BffTokenSet currentTokens = new(
            "access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
            UserAgent = "TestBrowser/1.0",
            SessionCreatedAt = BffEndpointsTestServer.FixedNow,
        };

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(currentTokens);

        _server.TokenStore.GetSessionIdsByUserAsync(
                BffEndpointsTestServer.TestFrontendName,
                "user-42",
                Arg.Any<CancellationToken>())
            .Returns(new List<string> { TestSessionId });

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Get, "/app/bff/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content
            .ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        JsonElement sessions = result.GetProperty("sessions");
        sessions.GetArrayLength().ShouldBe(1);
        sessions[0].GetProperty("isCurrent").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetSessions_WithoutSessionCookie_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/app/bff/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteSession_TargetSessionExists_Returns204()
    {
        string targetSessionId = "target-session-to-revoke";

        BffTokenSet currentTokens = new(
            "access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
        };

        BffTokenSet targetTokens = new(
            "target-access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
        };

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(currentTokens);

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                targetSessionId,
                Arg.Any<CancellationToken>())
            .Returns(targetTokens);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Delete, $"/app/bff/sessions/{targetSessionId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.TokenStore.Received(1).RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            targetSessionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSession_TargetSessionNotFound_Returns404()
    {
        BffTokenSet currentTokens = new(
            "access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
        };

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(currentTokens);

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                "nonexistent-session",
                Arg.Any<CancellationToken>())
            .Returns((BffTokenSet?)null);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Delete, "/app/bff/sessions/nonexistent-session");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_TargetBelongsToDifferentUser_Returns404()
    {
        string targetSessionId = "other-user-session";

        BffTokenSet currentTokens = new(
            "access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
        };

        BffTokenSet targetTokens = new(
            "target-access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "different-user-99",
        };

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(currentTokens);

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                targetSessionId,
                Arg.Any<CancellationToken>())
            .Returns(targetTokens);

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Delete, $"/app/bff/sessions/{targetSessionId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Should not remove the session since it belongs to a different user
        await _server.TokenStore.DidNotReceive().RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            targetSessionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSession_WithoutSessionCookie_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.DeleteAsync(
            "/app/bff/sessions/some-session-id", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAllSessions_WithoutSessionCookie_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.DeleteAsync(
            "/app/bff/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAllSessions_WithSessionCookie_Returns204()
    {
        string otherSessionId = "other-session-id";

        BffTokenSet currentTokens = new(
            "access-token", null, null, BffEndpointsTestServer.FixedNow.AddHours(1))
        {
            UserId = "user-42",
        };

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(currentTokens);

        _server.TokenStore.GetSessionIdsByUserAsync(
                BffEndpointsTestServer.TestFrontendName,
                "user-42",
                Arg.Any<CancellationToken>())
            .Returns(new List<string> { TestSessionId, otherSessionId });

        HttpResponseMessage response = await SendAnonymousWithSessionAsync(
            HttpMethod.Delete, "/app/bff/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Only the other session should be revoked, not the current one
        await _server.TokenStore.Received(1).RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            otherSessionId,
            Arg.Any<CancellationToken>());

        await _server.TokenStore.DidNotReceive().RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            TestSessionId,
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Back-channel logout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostBackChannelLogout_ValidLogoutToken_Returns200()
    {
        ValidatedLogoutToken validatedToken = new(
            Issuer: "https://auth.example.com",
            Subject: "user-42",
            Jti: "unique-jti-123",
            HasBackChannelLogoutEvent: true);

        _server.LogoutTokenValidator.ValidateAsync(
                "valid-logout-token",
                "test-client-id",
                Arg.Any<CancellationToken>())
            .Returns(validatedToken);

        _server.Cache.TryGetAsync<bool>(
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new MaybeValue<bool>());

        _server.TokenStore.GetSessionIdsByUserAsync(
                BffEndpointsTestServer.TestFrontendName,
                "user-42",
                Arg.Any<CancellationToken>())
            .Returns(new List<string> { "session-1", "session-2" });

        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["logout_token"] = "valid-logout-token",
        });

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/app/bff/backchannel-logout", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Both sessions should be revoked
        await _server.TokenStore.Received(1).RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            "session-1",
            Arg.Any<CancellationToken>());

        await _server.TokenStore.Received(1).RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            "session-2",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostBackChannelLogout_MissingLogoutToken_Returns400()
    {
        // Post an empty form without the logout_token parameter
        FormUrlEncodedContent content = new(new Dictionary<string, string>());

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/app/bff/backchannel-logout", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBackChannelLogout_InvalidLogoutToken_Returns400()
    {
        _server.LogoutTokenValidator.ValidateAsync(
                "invalid-token",
                "test-client-id",
                Arg.Any<CancellationToken>())
            .Returns((ValidatedLogoutToken?)null);

        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["logout_token"] = "invalid-token",
        });

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/app/bff/backchannel-logout", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBackChannelLogout_ReplayedJti_Returns400()
    {
        ValidatedLogoutToken validatedToken = new(
            Issuer: "https://auth.example.com",
            Subject: "user-42",
            Jti: "replayed-jti",
            HasBackChannelLogoutEvent: true);

        _server.LogoutTokenValidator.ValidateAsync(
                "replayed-logout-token",
                "test-client-id",
                Arg.Any<CancellationToken>())
            .Returns(validatedToken);

        // Simulate the jti already being cached (replay scenario)
        _server.Cache.TryGetAsync<bool>(
                "bff:bc-logout-jti:replayed-jti",
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(MaybeValue<bool>.FromValue(true));

        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["logout_token"] = "replayed-logout-token",
        });

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/app/bff/backchannel-logout", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Logout endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetLogout_WithSession_ClearsSessionAndRedirects()
    {
        BffTokenSet tokens = new(
            "access-token", "refresh-token", "id-token-hint",
            BffEndpointsTestServer.FixedNow.AddHours(1));

        _server.TokenStore.GetAsync(
                BffEndpointsTestServer.TestFrontendName,
                TestSessionId,
                Arg.Any<CancellationToken>())
            .Returns(tokens);

        // Use a non-redirect-following client to capture the 302
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            CreateRequestWithSession(HttpMethod.Get, "/app/bff/logout"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        string redirectUrl = response.Headers.Location.ToString();
        redirectUrl.ShouldContain("auth.example.com/connect/logout");
        redirectUrl.ShouldContain("client_id=test-client-id");
        redirectUrl.ShouldContain("id_token_hint=");

        await _server.TokenStore.Received(1).RemoveAsync(
            BffEndpointsTestServer.TestFrontendName,
            TestSessionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLogout_WithoutSession_RedirectsWithoutSessionCleanup()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/logout"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain("auth.example.com/connect/logout");

        // No session removal should occur since there was no session cookie
        await _server.TokenStore.DidNotReceive().RemoveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Login endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetLogin_RedirectsToAuthorizeEndpointWithPkceParams()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/login"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        string redirectUrl = response.Headers.Location!.ToString();
        redirectUrl.ShouldContain("auth.example.com/connect/authorize");
        redirectUrl.ShouldContain("client_id=test-client-id");
        redirectUrl.ShouldContain("response_type=code");
        redirectUrl.ShouldContain("code_challenge=");
        redirectUrl.ShouldContain("code_challenge_method=S256");
        redirectUrl.ShouldContain("state=");
        redirectUrl.ShouldContain("scope=openid");
    }

    [Fact]
    public async Task GetLogin_StoresPkceStateInCache()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/login"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        await _server.Cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.StartsWith("bff:pkce:")),
            Arg.Any<BffLoginEndpoints.PkceState>(),
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLogin_RedirectUriContainsCallbackPath()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/login"),
            TestContext.Current.CancellationToken);

        string redirectUrl = response.Headers.Location!.ToString();
        // The redirect_uri should point back to /app/bff/callback
        string decodedUrl = HttpUtility.UrlDecode(redirectUrl);
        decodedUrl.ShouldContain("/app/bff/callback");
    }

    // -------------------------------------------------------------------------
    // Callback endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCallback_ValidCodeAndState_ExchangesTokensAndRedirects()
    {
        string testState = "test-state-123";
        string testCode = "authorization-code-456";

        // Pre-populate cache with PkceState
        BffLoginEndpoints.PkceState pkceState = new("test-verifier", testState, BffEndpointsTestServer.TestFrontendName);
        _server.Cache.TryGetAsync<BffLoginEndpoints.PkceState>(default!, default, CancellationToken.None)
            .ReturnsForAnyArgs(MaybeValue<BffLoginEndpoints.PkceState>.FromValue(pkceState));

        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/app/bff/callback?code={testCode}&state={testState}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // Verify tokens were stored
        await _server.TokenStore.Received(1).StoreAsync(
            BffEndpointsTestServer.TestFrontendName,
            Arg.Any<string>(),
            Arg.Is<BffTokenSet>(t => t.AccessToken == "mock-access-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCallback_TokenExchangeFails_RedirectsToErrorPage()
    {
        string testState = "test-state-fail";

        BffLoginEndpoints.PkceState pkceState = new("test-verifier", testState, BffEndpointsTestServer.TestFrontendName);
        _server.Cache.TryGetAsync<BffLoginEndpoints.PkceState>(default!, default, CancellationToken.None)
            .ReturnsForAnyArgs(MaybeValue<BffLoginEndpoints.PkceState>.FromValue(pkceState));

        // Mock token endpoint to return failure
        _server.TokenEndpointHandler.TokenStatusCode = HttpStatusCode.BadRequest;
        _server.TokenEndpointHandler.TokenResponse = """{"error":"invalid_grant"}""";

        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/app/bff/callback?code=bad-code&state={testState}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/app/login?error=token_exchange_failed");
    }

    [Fact]
    public async Task GetCallback_MissingCode_RedirectsToErrorPage()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/callback?state=some-state"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/app/login?error=missing_code_or_state");
    }

    [Fact]
    public async Task GetCallback_MissingState_RedirectsToErrorPage()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/callback?code=some-code"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/app/login?error=missing_code_or_state");
    }

    [Fact]
    public async Task GetCallback_InvalidState_RedirectsToErrorPage()
    {
        // Cache returns empty (state not found)
        _server.Cache.TryGetAsync<BffLoginEndpoints.PkceState>(
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new MaybeValue<BffLoginEndpoints.PkceState>());

        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/callback?code=test-code&state=unknown-state"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/app/login?error=invalid_state");
    }

    [Fact]
    public async Task GetCallback_OidcError_RedirectsWithSafeErrorCode()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/callback?error=access_denied"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/app/login?error=access_denied");
    }

    [Fact]
    public async Task GetCallback_UnknownOidcError_RedirectsWithUnknownError()
    {
        HttpResponseMessage response = await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, "/app/bff/callback?error=xss_attempt"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string location = response.Headers.Location!.ToString();
        location.ShouldContain("error=unknown_error");
        location.ShouldNotContain("xss_attempt");
    }

    [Fact]
    public async Task GetCallback_ValidResponse_ConsumesStateFromCache()
    {
        string testState = "consumed-state";

        BffLoginEndpoints.PkceState pkceState = new("verifier", testState, BffEndpointsTestServer.TestFrontendName);
        _server.Cache.TryGetAsync<BffLoginEndpoints.PkceState>(default!, default, CancellationToken.None)
            .ReturnsForAnyArgs(MaybeValue<BffLoginEndpoints.PkceState>.FromValue(pkceState));

        await _server.SendWithoutRedirectAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/app/bff/callback?code=code&state={testState}"),
            TestContext.Current.CancellationToken);

        // Verify the PKCE state was removed from cache (one-time use)
        await _server.Cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.Contains(testState)),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }
}
