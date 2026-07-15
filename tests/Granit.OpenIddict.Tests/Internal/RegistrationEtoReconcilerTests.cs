using Granit.Events;
using Granit.Identity.Local.Events;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.OpenIddict.Tests.Internal;

/// <summary>
/// The reconciler publishes a <see cref="UserRegisteredEto"/> for every pending registration and
/// marks each dispatched — publishing before marking so a crash between the two re-publishes
/// (at-least-once) rather than dropping the registration side effects.
/// </summary>
public sealed class RegistrationEtoReconcilerTests
{
    [Fact]
    public async Task ReconcileAsync_PublishesAndMarksEachPending()
    {
        PendingRegistration tenantUser = new(Guid.NewGuid(), Guid.NewGuid());
        PendingRegistration globalUser = new(Guid.NewGuid(), null);

        IPendingRegistrationStore store = Substitute.For<IPendingRegistrationStore>();
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([tenantUser, globalUser]);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();

        RegistrationEtoReconciler reconciler = new(
            store, bus, NullLogger<RegistrationEtoReconciler>.Instance);

        await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(
            Arg.Is<UserRegisteredEto>(e => e.UserId == tenantUser.UserId && e.TenantId == tenantUser.TenantId),
            Arg.Any<CancellationToken>());
        await bus.Received(1).PublishAsync(
            Arg.Is<UserRegisteredEto>(e => e.UserId == globalUser.UserId && e.TenantId == null),
            Arg.Any<CancellationToken>());
        await store.Received(1).MarkDispatchedAsync(tenantUser.UserId, Arg.Any<CancellationToken>());
        await store.Received(1).MarkDispatchedAsync(globalUser.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileAsync_NoPending_PublishesNothing()
    {
        IPendingRegistrationStore store = Substitute.For<IPendingRegistrationStore>();
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();

        RegistrationEtoReconciler reconciler = new(
            store, bus, NullLogger<RegistrationEtoReconciler>.Instance);

        await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        await bus.DidNotReceive().PublishAsync(Arg.Any<UserRegisteredEto>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkDispatchedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
