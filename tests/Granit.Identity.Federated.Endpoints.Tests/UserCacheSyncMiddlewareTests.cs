using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Endpoints.Tests;

/// <summary>
/// Behaviour cover for the login-time <see cref="UserCacheSyncMiddleware"/>. When the cache entry is
/// missing or stale it routes the claim-derived user through the shared <see cref="IFederatedIdentityWriter"/>
/// (ADR-051 joint hydration), and short-circuits for local stores, unauthenticated requests, disabled sync,
/// and fresh entries.
/// </summary>
public sealed class UserCacheSyncMiddlewareTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly IFederatedIdentityWriter _writer = Substitute.For<IFederatedIdentityWriter>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly FakeTimeProvider _time = new();
    private bool _nextCalled;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task Invoke(UserCacheOptions? options = null)
    {
        var middleware = new UserCacheSyncMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });
        return middleware.InvokeAsync(
            new DefaultHttpContext(),
            _currentUser,
            _currentTenant,
            _store,
            _writer,
            _time,
            Microsoft.Extensions.Options.Options.Create(options ?? new UserCacheOptions()),
            _capabilities);
    }

    [Fact]
    public async Task SkipsSync_WhenProviderIsLocalStore()
    {
        _capabilities.IsLocalStore.Returns(true);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");

        await Invoke();

        _nextCalled.ShouldBeTrue();
        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default, Ct);
    }

    [Fact]
    public async Task SkipsSync_WhenNotAuthenticated()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(false);

        await Invoke();

        _nextCalled.ShouldBeTrue();
        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default, Ct);
    }

    [Fact]
    public async Task WritesThroughWriter_WhenNoCachedEntry()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        _currentUser.Email.Returns("jane@test.com");
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);

        await Invoke();

        await _writer.Received(1).WriteAsync(
            Arg.Is<IIdentityUser>(u => u.UserId == "user-1" && u.Email == "jane@test.com"),
            null,
            Arg.Any<CancellationToken>());
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipsWrite_WhenCachedEntryIsFresh()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new FederatedIdentity { ExternalUserId = "user-1", LastSyncedAt = _time.GetUtcNow() });

        await Invoke(new UserCacheOptions { StalenessThreshold = TimeSpan.FromHours(24) });

        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default, Ct);
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task WritesThroughWriter_WhenCachedEntryIsStale()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        FederatedIdentity stale = new()
        {
            ExternalUserId = "user-1",
            LastSyncedAt = _time.GetUtcNow() - TimeSpan.FromDays(2),
        };
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(stale);

        await Invoke(new UserCacheOptions { StalenessThreshold = TimeSpan.FromHours(24) });

        // Passes the stale row so the writer takes the update path (preserving the id pair).
        await _writer.Received(1).WriteAsync(
            Arg.Is<IIdentityUser>(u => u.UserId == "user-1"), stale, Arg.Any<CancellationToken>());
    }
}
