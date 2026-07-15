using Granit.Events;
using Granit.Identity.Local.Events;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.OpenIddict.Tests.Internal;

/// <summary>
/// The reconciler publishes an <see cref="AccountDeletedEto"/> for every pending deletion and marks
/// each dispatched — publishing before marking so a crash between the two re-publishes (at-least-once)
/// rather than dropping the erasure event.
/// </summary>
public sealed class AccountDeletionEtoReconcilerTests
{
    [Fact]
    public async Task ReconcileAsync_PublishesAndMarksEachPending()
    {
        PendingAccountDeletion tenantUser = new(Guid.NewGuid(), Guid.NewGuid());
        PendingAccountDeletion globalUser = new(Guid.NewGuid(), null);

        IPendingAccountDeletionStore store = Substitute.For<IPendingAccountDeletionStore>();
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([tenantUser, globalUser]);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UnixEpoch);

        AccountDeletionEtoReconciler reconciler = new(
            store, bus, clock, NullLogger<AccountDeletionEtoReconciler>.Instance);

        await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(
            Arg.Is<AccountDeletedEto>(e => e.UserId == tenantUser.UserId && e.TenantId == tenantUser.TenantId),
            Arg.Any<CancellationToken>());
        await bus.Received(1).PublishAsync(
            Arg.Is<AccountDeletedEto>(e => e.UserId == globalUser.UserId && e.TenantId == null),
            Arg.Any<CancellationToken>());
        await store.Received(1).MarkDispatchedAsync(
            tenantUser.UserId, DateTimeOffset.UnixEpoch, Arg.Any<CancellationToken>());
        await store.Received(1).MarkDispatchedAsync(
            globalUser.UserId, DateTimeOffset.UnixEpoch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_NoPending_PublishesNothing()
    {
        IPendingAccountDeletionStore store = Substitute.For<IPendingAccountDeletionStore>();
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();

        AccountDeletionEtoReconciler reconciler = new(
            store, bus, Substitute.For<IClock>(), NullLogger<AccountDeletionEtoReconciler>.Instance);

        await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        await bus.DidNotReceive().PublishAsync(Arg.Any<AccountDeletedEto>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkDispatchedAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
