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
/// Behaviour cover for the login-time <see cref="UserCacheSyncMiddleware"/> after its move out of the
/// federated domain package into this ASP.NET Core integration package (Vague 4a). The middleware upserts
/// the current authenticated user's cache entry from JWT claims when the entry is missing or stale, and
/// short-circuits for local stores, unauthenticated requests, disabled sync, and fresh entries.
/// </summary>
public sealed class UserCacheSyncMiddlewareTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
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
        await _store.DidNotReceiveWithAnyArgs().UpsertAsync(default!, Ct);
    }

    [Fact]
    public async Task SkipsSync_WhenNotAuthenticated()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(false);

        await Invoke();

        _nextCalled.ShouldBeTrue();
        await _store.DidNotReceiveWithAnyArgs().UpsertAsync(default!, Ct);
    }

    [Fact]
    public async Task Upserts_WhenNoCachedEntry()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        _currentUser.Email.Returns("jane@test.com");
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);

        await Invoke();

        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.ExternalUserId == "user-1" && e.Email == "jane@test.com"),
            Arg.Any<CancellationToken>());
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipsUpsert_WhenCachedEntryIsFresh()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new FederatedIdentity { ExternalUserId = "user-1", LastSyncedAt = _time.GetUtcNow() });

        await Invoke(new UserCacheOptions { StalenessThreshold = TimeSpan.FromHours(24) });

        await _store.DidNotReceiveWithAnyArgs().UpsertAsync(default!, Ct);
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Upserts_WhenCachedEntryIsStale()
    {
        _capabilities.IsLocalStore.Returns(false);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("user-1");
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new FederatedIdentity
            {
                ExternalUserId = "user-1",
                LastSyncedAt = _time.GetUtcNow() - TimeSpan.FromDays(2),
            });

        await Invoke(new UserCacheOptions { StalenessThreshold = TimeSpan.FromHours(24) });

        await _store.Received(1).UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }
}
