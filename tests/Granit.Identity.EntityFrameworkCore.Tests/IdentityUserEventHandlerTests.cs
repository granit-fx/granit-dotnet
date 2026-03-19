using Granit.Core.Events;
using Granit.Identity.EntityFrameworkCore.Entities;
using Granit.Identity.EntityFrameworkCore.Events;
using Granit.Identity.EntityFrameworkCore.Handlers;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class IdentityUserEventHandlerTests
{
    private readonly IIdentityProvider _provider = Substitute.For<IIdentityProvider>();
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly ILocalEventBus _localEventBus = Substitute.For<ILocalEventBus>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private IdentityUserEventHandler CreateHandler() => new(
        _provider, _store, _localEventBus, _distributedEventBus, _timeProvider,
        NullLogger<IdentityUserEventHandler>.Instance);

    public IdentityUserEventHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HandleUpdated_FetchesAndUpsertsCache()
    {
        var user = new IdentityUser("user-1", "jdoe", "jdoe@test.com", "John", "Doe", true);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEvent("user-1"), TestContext.Current.CancellationToken);

        await _store.Received(1).UpsertAsync(
            Arg.Is<UserCacheEntry>(e => e.ExternalUserId == "user-1" && e.Username == "jdoe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleUpdated_DoesNotUpsert_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((IdentityUser?)null);

        IdentityUserEventHandler handler = CreateHandler();
        await handler.HandleAsync(
            new IdentityUserUpdatedEvent("user-1"), TestContext.Current.CancellationToken);

        await _store.DidNotReceive().UpsertAsync(
            Arg.Any<UserCacheEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleDeleted_DeletesCacheEntry()
    {
        var tenantId = Guid.NewGuid();
        IdentityUserEventHandler handler = CreateHandler();

        await handler.HandleAsync(
            new IdentityUserDeletedEvent("user-1", tenantId), TestContext.Current.CancellationToken);

        await _store.Received(1).DeleteByExternalIdAsync("user-1", tenantId, Arg.Any<CancellationToken>());
    }
}
