using Granit.Identity.Local.Events;
using Granit.Presence.Abstractions;
using Granit.Presence.Wolverine.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Wolverine.Tests.Handlers;

public sealed class AccountDeletedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PurgesOverrideAndHeartbeat()
    {
        IPresenceStore store = Substitute.For<IPresenceStore>();
        IPresenceTracker tracker = Substitute.For<IPresenceTracker>();
        var userId = Guid.NewGuid();
        AccountDeletedEto evt = new(userId, TenantId: null);

        await AccountDeletedHandler.HandleAsync(
            evt, store, tracker, NullLogger<AccountDeletedHandler>.Instance, TestContext.Current.CancellationToken);

        await store.Received(1).DeleteAsync(userId, Arg.Any<CancellationToken>());
        await tracker.Received(1).RemoveAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Idempotent_WhenInvokedTwice()
    {
        IPresenceStore store = Substitute.For<IPresenceStore>();
        IPresenceTracker tracker = Substitute.For<IPresenceTracker>();
        var userId = Guid.NewGuid();
        AccountDeletedEto evt = new(userId, TenantId: Guid.NewGuid());

        await AccountDeletedHandler.HandleAsync(
            evt, store, tracker, NullLogger<AccountDeletedHandler>.Instance, TestContext.Current.CancellationToken);
        await AccountDeletedHandler.HandleAsync(
            evt, store, tracker, NullLogger<AccountDeletedHandler>.Instance, TestContext.Current.CancellationToken);

        await store.Received(2).DeleteAsync(userId, Arg.Any<CancellationToken>());
        await tracker.Received(2).RemoveAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullMessage_Throws()
    {
        IPresenceStore store = Substitute.For<IPresenceStore>();
        IPresenceTracker tracker = Substitute.For<IPresenceTracker>();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await AccountDeletedHandler.HandleAsync(
                null!, store, tracker, NullLogger<AccountDeletedHandler>.Instance, CancellationToken.None));
    }
}
