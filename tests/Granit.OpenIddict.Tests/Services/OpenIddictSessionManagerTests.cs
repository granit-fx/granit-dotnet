using System.Collections.Immutable;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Granit.OpenIddict.Services;
using Granit.Timing;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.Tests.Services;

public sealed class OpenIddictSessionManagerTests
{
    private static readonly DateTimeOffset FixedNow = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IOpenIddictApplicationManager _appManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();

    public OpenIddictSessionManagerTests()
    {
        _clock.Now.Returns(FixedNow);
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
    }

    private OpenIddictSessionManager CreateSut() =>
        new(_tokenManager, _appManager, _cache, _clock, _dataFilter);

    // ── Multi-tenant filter bypass ────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_DisablesMultiTenantFilter()
    {
        const string userId = "user-mt";
        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());

        await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        _dataFilter.Received(1).Disable<IMultiTenant>();
    }

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_ValidRefreshToken_ReturnsSession()
    {
        const string userId = "user-1";
        const string sessionId = "tok-1";
        const string ipAddress = "1.2.3.4";
        DateTimeOffset createdAt = new(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset lastActivity = new(2025, 1, 10, 14, 0, 0, TimeSpan.Zero);

        object token = new();
        SetupValidRefreshToken(token, sessionId, createdAt, appId: null);
        SetupTokenProperties(token, ipAddress);
        SetupCacheHit(userId, sessionId, lastActivity);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<IdentitySession> result =
            await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SessionId.ShouldBe(sessionId);
        result[0].IpAddress.ShouldBe(ipAddress);
        result[0].StartedAt.ShouldBe(createdAt);
        result[0].LastAccess.ShouldBe(lastActivity);
        result[0].RememberMe.ShouldBeFalse();
    }

    // ── Cache miss fallback ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_CacheMiss_LastAccessFallsBackToCreationDate()
    {
        const string userId = "user-2";
        const string sessionId = "tok-2";
        DateTimeOffset createdAt = new(2025, 2, 1, 8, 0, 0, TimeSpan.Zero);

        object token = new();
        SetupValidRefreshToken(token, sessionId, createdAt, appId: null);
        SetupTokenProperties(token, null);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));

        IReadOnlyList<IdentitySession> result =
            await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        result[0].LastAccess.ShouldBe(createdAt);
    }

    // ── Type filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_FiltersOutNonRefreshTokens()
    {
        const string userId = "user-3";
        const string sessionId = "tok-3";

        object accessToken = new();
        SetupTokenType(accessToken, OpenIddictConstants.TokenTypeHints.AccessToken);

        object refreshToken = new();
        SetupValidRefreshToken(refreshToken, sessionId, FixedNow, appId: null);
        SetupTokenProperties(refreshToken, null);
        SetupCacheMiss(userId, sessionId);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(accessToken, refreshToken));

        IReadOnlyList<IdentitySession> result =
            await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
    }

    // ── Status filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_FiltersOutRevokedTokens()
    {
        const string userId = "user-4";

        object revokedToken = new();
        SetupTokenType(revokedToken, OpenIddictConstants.TokenTypeHints.RefreshToken);
        SetupTokenStatus(revokedToken, OpenIddictConstants.Statuses.Revoked);

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(revokedToken));

        IReadOnlyList<IdentitySession> result =
            await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ── N+1 guard ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserSessionsAsync_SameApp_CallsGetDisplayNameOnce()
    {
        const string userId = "user-5";
        const string appId = "app-1";
        object app = new();

#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods
        _appManager.FindByIdAsync(appId, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<object?>(app));
        _appManager.GetDisplayNameAsync(app, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>("My App"));
#pragma warning restore CA2012

        object t1 = new(), t2 = new();
        SetupValidRefreshToken(t1, "tok-5a", FixedNow, appId);
        SetupValidRefreshToken(t2, "tok-5b", FixedNow, appId);
        SetupTokenProperties(t1, null);
        SetupTokenProperties(t2, null);
        SetupCacheMiss(userId, "tok-5a");
        SetupCacheMiss(userId, "tok-5b");

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(t1, t2));

        IReadOnlyList<IdentitySession> result =
            await CreateSut().GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(s => s.Clients.Contains("My App"));
        await _appManager.Received(1).GetDisplayNameAsync(app, Arg.Any<CancellationToken>());
    }

    // ── Device grouping ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUserDeviceActivityAsync_SameIp_ReturnsSingleDevice()
    {
        const string userId = "user-6";
        const string ip = "10.0.0.1";

        object t1 = new(), t2 = new();
        SetupValidRefreshToken(t1, "tok-6a", FixedNow, appId: null);
        SetupValidRefreshToken(t2, "tok-6b", FixedNow, appId: null);
        SetupTokenProperties(t1, ip);
        SetupTokenProperties(t2, ip);
        SetupCacheMiss(userId, "tok-6a");
        SetupCacheMiss(userId, "tok-6b");

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(t1, t2));

        IReadOnlyList<IdentityDeviceActivity> result =
            await CreateSut().GetUserDeviceActivityAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].IpAddress.ShouldBe(ip);
        result[0].Sessions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetUserDeviceActivityAsync_DifferentIps_ReturnsOneDeviceEach()
    {
        const string userId = "user-7";

        object t1 = new(), t2 = new();
        SetupValidRefreshToken(t1, "tok-7a", FixedNow, appId: null);
        SetupValidRefreshToken(t2, "tok-7b", FixedNow, appId: null);
        SetupTokenProperties(t1, "192.168.1.1");
        SetupTokenProperties(t2, "192.168.1.2");
        SetupCacheMiss(userId, "tok-7a");
        SetupCacheMiss(userId, "tok-7b");

        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(t1, t2));

        IReadOnlyList<IdentityDeviceActivity> result =
            await CreateSut().GetUserDeviceActivityAsync(userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods

    private void SetupTokenType(object token, string type) =>
        _tokenManager.GetTypeAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(type));

    private void SetupTokenStatus(object token, string status) =>
        _tokenManager.GetStatusAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(status));

    private void SetupValidRefreshToken(object token, string sessionId, DateTimeOffset createdAt, string? appId)
    {
        _tokenManager.GetTypeAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(OpenIddictConstants.TokenTypeHints.RefreshToken));
        _tokenManager.GetStatusAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(OpenIddictConstants.Statuses.Valid));
        _tokenManager.GetIdAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<string?>(sessionId));
        _tokenManager.GetCreationDateAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<DateTimeOffset?>(createdAt));
        _tokenManager.GetApplicationIdAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(appId));
    }

    private void SetupTokenProperties(object token, string? ipAddress)
    {
        ImmutableDictionary<string, JsonElement> props = ipAddress is not null
            ? ImmutableDictionary<string, JsonElement>.Empty.Add(
                "ip_address",
                JsonDocument.Parse($"\"{ipAddress}\"").RootElement)
            : ImmutableDictionary<string, JsonElement>.Empty;

        _tokenManager.GetPropertiesAsync(token, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(props));
    }

    private void SetupCacheHit(string userId, string sessionId, DateTimeOffset lastActivity) =>
        _cache.TryGetAsync<UserSessionActivity>(
            $"session:{userId}:{sessionId}",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<MaybeValue<UserSessionActivity>>(
                MaybeValue<UserSessionActivity>.FromValue(
                    new UserSessionActivity(userId, sessionId, lastActivity))));

    private void SetupCacheMiss(string userId, string sessionId) =>
        _cache.TryGetAsync<UserSessionActivity>(
            $"session:{userId}:{sessionId}",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<MaybeValue<UserSessionActivity>>(
                MaybeValue<UserSessionActivity>.None));

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
