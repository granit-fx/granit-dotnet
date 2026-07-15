using Granit.Events;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Handlers;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.RateLimiting;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Identity.Federated.Tests.Handlers;

public sealed class IdentityUserEventHandlerTests
{
    private readonly IIdentityProvider _provider = Substitute.For<IIdentityProvider>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly IFederatedIdentityWriter _writer = Substitute.For<IFederatedIdentityWriter>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ILocalEventBus _localEventBus = Substitute.For<ILocalEventBus>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly IUserSyncFailureRateLimiter _rateLimiter = Substitute.For<IUserSyncFailureRateLimiter>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private IdentityUserEventHandler CreateHandler() => new(
        _provider, _capabilities, _store, _writer, _currentTenant, _localEventBus, _distributedEventBus,
        _rateLimiter, _timeProvider, NullLogger<IdentityUserEventHandler>.Instance);

    public IdentityUserEventHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _capabilities.ProviderName.Returns("Keycloak");
        _rateLimiter.TryAcquire(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _writer.SyncAsync(Arg.Any<IIdentityUser>(), Arg.Any<CancellationToken>())
            .Returns(ci => new FederatedIdentity { ExternalUserId = ci.Arg<IIdentityUser>().UserId });
    }

    [Fact]
    public async Task HandleUpdated_FetchesAndSyncsThroughWriter()
    {
        var user = new FederatedIdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(user));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        // The webhook path funnels through the shared writer (ADR-051 joint hydration), never a
        // direct store.UpsertAsync that would insert a FederatedIdentity with UserId = Guid.Empty.
        await _writer.Received(1).SyncAsync(
            Arg.Is<IIdentityUser>(u => u.UserId == "user-1" && u.Username == "jdoe"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().UpsertAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleUpdated_DoesNotSync_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEto("user-1"), TestContext.Current.CancellationToken);

        await _writer.DidNotReceiveWithAnyArgs().SyncAsync(default!, TestContext.Current.CancellationToken);
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
    public async Task HandleDeleted_EstablishesTenantScope_BeforeDeleting()
    {
        // GDPR regression: distributed dispatch carries no ambient tenant, so without an
        // explicit scope the multi-tenant query filter hides the row and the delete
        // silently no-ops. The handler must establish the event's tenant before touching
        // the store.
        var tenantId = Guid.NewGuid();
        IdentityUserEventHandler handler = CreateHandler();

        await handler.HandleAsync(
            new IdentityUserDeletedEto("user-1", tenantId), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _currentTenant.Change(tenantId);
            _store.DeleteByExternalIdAsync("user-1", tenantId, Arg.Any<CancellationToken>());
        });
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
