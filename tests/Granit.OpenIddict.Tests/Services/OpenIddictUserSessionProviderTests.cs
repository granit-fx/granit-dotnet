using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity;
using Granit.OpenIddict.Services;
using Granit.Timing;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Services;

public sealed class OpenIddictUserSessionProviderTests
{
    private static readonly DateTimeOffset FixedNow = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IOpenIddictApplicationManager _applicationManager =
        Substitute.For<IOpenIddictApplicationManager>();
    private readonly IUserSessionActivityStore _activityStore = Substitute.For<IUserSessionActivityStore>();
    private readonly Dictionary<string, DateTimeOffset> _activities = [];
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();

    public OpenIddictUserSessionProviderTests()
    {
        _clock.Now.Returns(FixedNow);
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
        _activityStore.GetActivitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyDictionary<string, DateTimeOffset>)_activities);
    }

    private OpenIddictUserSessionProvider CreateSut() =>
        new(_tokenManager, _applicationManager, _activityStore, _clock, _dataFilter);

    // ── TouchAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task TouchAsync_WithAuthorization_TouchesTheActivityStore()
    {
        var authorizationId = Guid.NewGuid();
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(OpenIddictConstants.Claims.Private.AuthorizationId, authorizationId.ToString())]));

        await CreateSut().TouchAsync(principal, TestContext.Current.CancellationToken);

        await _activityStore.Received(1).TouchAsync(
            authorizationId, FixedNow, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TouchAsync_WithoutAuthorization_IsNoOp()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity()); // e.g. a client-credentials token

        await CreateSut().TouchAsync(principal, TestContext.Current.CancellationToken);

        await _activityStore.DidNotReceive().TouchAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // ── Multi-tenant filter bypass ────────────────────────────────────────

    [Fact]
    public async Task ListAsync_DisablesMultiTenantFilter()
    {
        const string userId = "user-mt";
        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());

        await CreateSut().ListAsync(userId, currentSessionId: null, TestContext.Current.CancellationToken);

        _dataFilter.Received(1).Disable<IMultiTenant>();
    }

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ValidRefreshToken_ReturnsSession()
    {
        const string userId = "user-1";
        const string sessionId = "tok-1";
        const string ipAddress = "1.2.3.4";
        DateTimeOffset createdAt = new(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset lastActivity = new(2025, 1, 10, 14, 0, 0, TimeSpan.Zero);

        object token = new();
        SetupValidRefreshToken(token, sessionId, createdAt, userId);
        SetupTokenProperties(token, ipAddress);
        SetupCacheHit(userId, sessionId, lastActivity);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, sessionId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SessionId.ShouldBe(sessionId);
        result[0].UserId.ShouldBe(userId);
        result[0].IsCurrent.ShouldBeTrue();
        result[0].IpAddress.ShouldBe(ipAddress);
        result[0].CreatedAt.ShouldBe(createdAt);
        result[0].LastAccessedAt.ShouldBe(lastActivity);
        result[0].Location.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_NonCurrentSession_IsCurrentFalse()
    {
        const string userId = "user-1b";
        const string sessionId = "tok-1b";

        object token = new();
        SetupValidRefreshToken(token, sessionId, FixedNow, userId);
        SetupTokenProperties(token, null);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, "other-session", TestContext.Current.CancellationToken);

        result[0].IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_CapturesUserAgentProperty()
    {
        const string userId = "user-ua";
        const string sessionId = "tok-ua";
        const string userAgent = "Mozilla/5.0";

        object token = new();
        SetupValidRefreshToken(token, sessionId, FixedNow, userId);
        SetupTokenProperties(token, ipAddress: null, userAgent: userAgent);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, null, TestContext.Current.CancellationToken);

        result[0].UserAgent.ShouldBe(userAgent);
    }

    // ── Cache miss fallback ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_CacheMiss_LastAccessFallsBackToCreationDate()
    {
        const string userId = "user-2";
        const string sessionId = "tok-2";
        DateTimeOffset createdAt = new(2025, 2, 1, 8, 0, 0, TimeSpan.Zero);

        object token = new();
        SetupValidRefreshToken(token, sessionId, createdAt, userId);
        SetupTokenProperties(token, null);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, null, TestContext.Current.CancellationToken);

        result[0].LastAccessedAt.ShouldBe(createdAt);
    }

    // ── Type filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_FiltersOutNonRefreshTokens()
    {
        const string userId = "user-3";
        const string sessionId = "tok-3";

        object accessToken = new();
        SetupTokenType(accessToken, OpenIddictConstants.TokenTypeHints.AccessToken);

        object refreshToken = new();
        SetupValidRefreshToken(refreshToken, sessionId, FixedNow, userId);
        SetupTokenProperties(refreshToken, null);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(accessToken, refreshToken));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, null, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    // ── Status filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_FiltersOutRevokedTokens()
    {
        const string userId = "user-4";

        object revokedToken = new();
        SetupTokenType(revokedToken, OpenIddictConstants.TokenTypeHints.RefreshToken);
        SetupTokenStatus(revokedToken, OpenIddictConstants.Statuses.Revoked);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(revokedToken));

        IReadOnlyList<UserSessionDescriptor> result =
            await CreateSut().ListAsync(userId, null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ── Device grouping ───────────────────────────────────────────────────

    [Fact]
    public async Task DeviceListAsync_SameIp_ReturnsSingleBrowserDevice()
    {
        const string userId = "user-6";
        const string ip = "10.0.0.1";

        object t1 = new(), t2 = new();
        SetupValidRefreshToken(t1, "tok-6a", FixedNow, userId);
        SetupValidRefreshToken(t2, "tok-6b", FixedNow, userId);
        SetupTokenProperties(t1, ip);
        SetupTokenProperties(t2, ip);
        SetupCacheMiss(userId, "tok-6a");
        SetupCacheMiss(userId, "tok-6b");

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(t1, t2));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].DeviceId.ShouldBe(ip);
        result[0].Kind.ShouldBe(DeviceKind.Browser);
        result[0].SessionCount.ShouldBe(2);
        result[0].LastLocation.ShouldBeNull();
    }

    [Fact]
    public async Task DeviceListAsync_DifferentIps_ReturnsOneDeviceEach()
    {
        const string userId = "user-7";

        object t1 = new(), t2 = new();
        SetupValidRefreshToken(t1, "tok-7a", FixedNow, userId);
        SetupValidRefreshToken(t2, "tok-7b", FixedNow, userId);
        SetupTokenProperties(t1, "192.168.1.1");
        SetupTokenProperties(t2, "192.168.1.2");
        SetupCacheMiss(userId, "tok-7a");
        SetupCacheMiss(userId, "tok-7b");

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(t1, t2));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    // ── Device kind: declared on the client wins over the heuristic ───────

    [Fact]
    public async Task DeviceListAsync_ClientDeclaresKind_SurfacesDeclaredKindOverHeuristic()
    {
        const string userId = "user-dk1";
        const string ip = "10.0.0.5";
        const string appId = "app-mobile";

        object token = new(), app = new();
        SetupValidRefreshToken(token, "tok-dk1", FixedNow, userId);
        SetupTokenProperties(token, ip);
        SetupCacheMiss(userId, "tok-dk1");
        SetupTokenApplication(token, appId, app);
        // Declared MobileApp, but the permissions would otherwise infer Tv — the declaration must win.
        SetupApplication(app, declaredKind: DeviceKind.MobileApp,
            permissions: [OpenIddictConstants.Permissions.GrantTypes.DeviceCode]);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.MobileApp);
    }

    // ── Device kind: heuristic fallback when the client declares nothing ──

    [Fact]
    public async Task DeviceListAsync_ExtensionRedirectUri_InfersBrowserExtension()
    {
        const string userId = "user-dk2";
        const string appId = "app-ext";

        object token = new(), app = new();
        SetupValidRefreshToken(token, "tok-dk2", FixedNow, userId);
        SetupTokenProperties(token, "10.0.0.6");
        SetupCacheMiss(userId, "tok-dk2");
        SetupTokenApplication(token, appId, app);
        SetupApplication(app, redirectUris: ["chrome-extension://abcdef/callback"]);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.BrowserExtension);
    }

    [Fact]
    public async Task DeviceListAsync_DeviceCodeGrant_InfersTv()
    {
        const string userId = "user-dk3";
        const string appId = "app-tv";

        object token = new(), app = new();
        SetupValidRefreshToken(token, "tok-dk3", FixedNow, userId);
        SetupTokenProperties(token, "10.0.0.7");
        SetupCacheMiss(userId, "tok-dk3");
        SetupTokenApplication(token, appId, app);
        SetupApplication(app, permissions: [OpenIddictConstants.Permissions.GrantTypes.DeviceCode]);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.Tv);
    }

    [Fact]
    public async Task DeviceListAsync_ClientCredentialsGrant_InfersApiClient()
    {
        const string userId = "user-dk4";
        const string appId = "app-api";

        object token = new(), app = new();
        SetupValidRefreshToken(token, "tok-dk4", FixedNow, userId);
        SetupTokenProperties(token, "10.0.0.8");
        SetupCacheMiss(userId, "tok-dk4");
        SetupTokenApplication(token, appId, app);
        SetupApplication(app, permissions: [OpenIddictConstants.Permissions.GrantTypes.ClientCredentials]);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.ApiClient);
    }

    [Fact]
    public async Task DeviceListAsync_PlainClient_DefaultsToBrowser()
    {
        const string userId = "user-dk5";
        const string appId = "app-web";

        object token = new(), app = new();
        SetupValidRefreshToken(token, "tok-dk5", FixedNow, userId);
        SetupTokenProperties(token, "10.0.0.9");
        SetupCacheMiss(userId, "tok-dk5");
        SetupTokenApplication(token, appId, app);
        SetupApplication(app, redirectUris: ["https://app.example.com/callback"],
            permissions: [OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode]);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<UserDevice> result =
            await CreateSut().ListAsync(userId, TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.Browser);
    }

    // ── Revocation (real, not a no-op) ────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_ValidTokenOwnedByUser_RevokesAndReturnsTrue()
    {
        const string userId = "user-r1";
        const string sessionId = "tok-r1";

        object token = new();
        SetupValidRefreshToken(token, sessionId, FixedNow, userId);
        SetupFindById(sessionId, token);
        SetupTryRevoke(token, true);

        bool result = await CreateSut().RevokeAsync(userId, sessionId, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        await _tokenManager.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_ReturnsFalseWithoutRevoking()
    {
        const string userId = "user-r2";

        SetupFindById("missing", null);

        bool result = await CreateSut().RevokeAsync(userId, "missing", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
        await _tokenManager.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedToken_ReturnsFalseWithoutRevoking()
    {
        const string userId = "user-r3";
        const string sessionId = "tok-r3";

        object token = new();
        SetupTokenType(token, OpenIddictConstants.TokenTypeHints.RefreshToken);
        SetupTokenStatus(token, OpenIddictConstants.Statuses.Revoked);
        SetupFindById(sessionId, token);

        bool result = await CreateSut().RevokeAsync(userId, sessionId, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
        await _tokenManager.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_TokenOwnedByAnotherSubject_ReturnsFalseWithoutRevoking()
    {
        const string userId = "user-r4";
        const string sessionId = "tok-r4";

        object token = new();
        SetupValidRefreshToken(token, sessionId, FixedNow, subject: "someone-else");
        SetupFindById(sessionId, token);

        bool result = await CreateSut().RevokeAsync(userId, sessionId, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
        await _tokenManager.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ── Revoke others ─────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeOthersAsync_RevokesEveryTokenExceptCurrent_ReturnsCount()
    {
        const string userId = "user-r5";
        const string current = "tok-current";

        object current_t = new(), other1 = new(), other2 = new();
        SetupValidRefreshToken(current_t, current, FixedNow, userId);
        SetupValidRefreshToken(other1, "tok-o1", FixedNow, userId);
        SetupValidRefreshToken(other2, "tok-o2", FixedNow, userId);
        SetupTokenProperties(current_t, null);
        SetupTokenProperties(other1, null);
        SetupTokenProperties(other2, null);
        SetupCacheMiss(userId, current);
        SetupCacheMiss(userId, "tok-o1");
        SetupCacheMiss(userId, "tok-o2");

        // RevokeAsync re-fetches each session by id.
        SetupFindById("tok-o1", other1);
        SetupFindById("tok-o2", other2);
        SetupTryRevoke(other1, true);
        SetupTryRevoke(other2, true);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(current_t, other1, other2));

        int revoked = await CreateSut().RevokeOthersAsync(userId, current, TestContext.Current.CancellationToken);

        revoked.ShouldBe(2);
        await _tokenManager.DidNotReceive().TryRevokeAsync(current_t, Arg.Any<CancellationToken>());
        await _tokenManager.Received(1).TryRevokeAsync(other1, Arg.Any<CancellationToken>());
        await _tokenManager.Received(1).TryRevokeAsync(other2, Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────

#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods

    private void SetupTokenType(object token, string type) =>
        _tokenManager.GetTypeAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(type));

    private void SetupTokenStatus(object token, string status) =>
        _tokenManager.GetStatusAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(status));

    private void SetupFindById(string id, object? token) =>
        _tokenManager.FindByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(token));

    private void SetupTryRevoke(object token, bool result) =>
        _tokenManager.TryRevokeAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(result));

    private void SetupValidRefreshToken(object token, string sessionId, DateTimeOffset createdAt, string subject)
    {
        _tokenManager.GetTypeAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(OpenIddictConstants.TokenTypeHints.RefreshToken));
        _tokenManager.GetStatusAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(OpenIddictConstants.Statuses.Valid));
        _tokenManager.GetIdAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(sessionId));
        _tokenManager.GetCreationDateAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<DateTimeOffset?>(createdAt));
        _tokenManager.GetSubjectAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(subject));
    }

    private void SetupTokenProperties(object token, string? ipAddress, string? userAgent = null)
    {
        ImmutableDictionary<string, JsonElement> props = ImmutableDictionary<string, JsonElement>.Empty;
        if (ipAddress is not null)
        {
            props = props.Add("ip_address", JsonDocument.Parse($"\"{ipAddress}\"").RootElement);
        }

        if (userAgent is not null)
        {
            props = props.Add("user_agent", JsonDocument.Parse($"\"{userAgent}\"").RootElement);
        }

        _tokenManager.GetPropertiesAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(props));
    }

    // Records last-activity for a session in the batched activity map the store returns.
    private void SetupCacheHit(string userId, string sessionId, DateTimeOffset lastActivity) =>
        _activities[sessionId] = lastActivity;

    // No stored activity — the session simply is not in the map, so the provider falls back to CreatedAt.
    private static void SetupCacheMiss(string userId, string sessionId)
    {
    }

    private void SetupTokenApplication(object token, string applicationId, object application)
    {
        _tokenManager.GetApplicationIdAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(applicationId));
        _applicationManager.FindByIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(application));
    }

    // Mocks the manager-level reads the DeviceKind extension methods delegate to: the declared kind lives in
    // the Properties bag; the heuristic reads redirect URIs and permissions.
    private void SetupApplication(
        object application,
        DeviceKind? declaredKind = null,
        string[]? redirectUris = null,
        string[]? permissions = null)
    {
        ImmutableDictionary<string, JsonElement> props = ImmutableDictionary<string, JsonElement>.Empty;
        if (declaredKind is not null)
        {
            props = props.Add(
                "urn:granit:openiddict:device_kind",
                JsonSerializer.SerializeToElement(declaredKind.Value.ToString()));
        }

        _applicationManager.GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(props));
        _applicationManager.GetRedirectUrisAsync(application, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(ImmutableArray.Create(redirectUris ?? [])));
        _applicationManager.GetPermissionsAsync(application, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(ImmutableArray.Create(permissions ?? [])));
    }

#pragma warning restore CA2012

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
