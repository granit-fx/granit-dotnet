using Granit.Events;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Handlers;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Identity.Federated.Tests.Handlers;

public sealed class IdentityUserEventHandlerTests
{
    private readonly IIdentityProvider _provider = Substitute.For<IIdentityProvider>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly ILocalEventBus _localEventBus = Substitute.For<ILocalEventBus>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly IUserSyncFailureRateLimiter _rateLimiter = Substitute.For<IUserSyncFailureRateLimiter>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private IdentityUserEventHandler CreateHandler() => new(
        _provider, _capabilities, _store, _localEventBus, _distributedEventBus, _rateLimiter,
        _timeProvider, NullLogger<IdentityUserEventHandler>.Instance);

    public IdentityUserEventHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _capabilities.ProviderName.Returns("Keycloak");
        _rateLimiter.TryAcquire(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task HandleUpdated_FetchesAndUpsertsCache()
    {
        var user = new FederatedIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(user));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.ExternalUserId == "user-1" && e.Username == "jdoe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleUpdated_DoesNotUpsert_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _store.DidNotReceive().UpsertAsync(
            Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleDeleted_DeletesCacheEntry()
    {
        var tenantId = Guid.NewGuid();
        IdentityUserEventHandler handler = CreateHandler();

        await handler.HandleAsync(
            new IdentityUserDeletedEto("user-1", tenantId), TestContext.Current.CancellationToken);

        await _store.Received(1).DeleteByExternalIdAsync("user-1", tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleUpdated_EmitsSyncFailedEto_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserSyncFailedEto>(e =>
                e.UserId == "user-1"
                && e.ProviderName == "Keycloak"
                && !string.IsNullOrWhiteSpace(e.Reason)),
            Arg.Any<CancellationToken>());
        _rateLimiter.Received(1).TryAcquire("user-1", "Keycloak");
    }

    [Fact]
    public async Task HandleUpdated_DoesNotEmitSyncFailedEto_WhenRateLimited()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));
        _rateLimiter.TryAcquire("user-1", "Keycloak").Returns(false);

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _distributedEventBus.DidNotReceive().PublishAsync(
            Arg.Any<IdentityUserSyncFailedEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleUpdated_DoesNotEmitSyncFailedEto_OnSuccessfulSync()
    {
        var user = new FederatedIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(user));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _distributedEventBus.DidNotReceive().PublishAsync(
            Arg.Any<IdentityUserSyncFailedEto>(), Arg.Any<CancellationToken>());
    }
}
