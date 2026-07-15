using Granit.Identity.Federated.Privacy.DataDeletion;
using Granit.Identity.Federated.Privacy.DataExport;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Privacy.Tests.DataDeletion;

public sealed class IdentityFederatedPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_erases_the_cached_entry_for_the_event_tenant()
    {
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        await IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(Tenant), cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        await cacheEraser.Received(1).EraseAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_establishes_the_event_tenant_scope_before_erasing()
    {
        // GDPR regression: distributed dispatch carries no ambient tenant, so without an
        // explicit scope the multi-tenant query filter hides the mirror row and the
        // erasure silently no-ops while the saga acks the deletion.
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        await IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(Tenant), cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            currentTenant.Change(Tenant);
            cacheEraser.EraseAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(Tenant);

        await IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        await cacheEraser.Received(1).EraseAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_sweeps_all_tenants_when_no_scope_is_resolvable()
    {
        // No tenant on the event and none ambient: Art. 17 must not leave a mirror behind
        // in any partition — the eraser is invoked with a null scope (all-tenants sweep).
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);

        await IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        await cacheEraser.Received(1).EraseAsync(User.ToString(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_reports_the_erased_row_count_in_the_acknowledgement()
    {
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        cacheEraser.EraseAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(3);

        PersonalDataDeletedEto ack = await IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(Tenant), cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        ack.AffectedRecords.ShouldBe(3);
    }

    [Fact]
    public async Task Handle_acknowledges_with_the_registered_provider_name_and_request_id()
    {
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);

        PersonalDataDeletedEto ack = await IdentityFederatedPersonalDataDeletionHandler.Handle(
            eto, cacheEraser, currentTenant, TestContext.Current.CancellationToken);

        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(IdentityFederatedPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.PhysicalDelete);
        ack.TenantId.ShouldBe(Tenant);
    }

    [Fact]
    public async Task Handle_does_not_acknowledge_when_the_erasure_fails()
    {
        IFederatedUserCacheEraser cacheEraser = Substitute.For<IFederatedUserCacheEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        cacheEraser.EraseAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("cache erase failed"));

        await Should.ThrowAsync<InvalidOperationException>(() => IdentityFederatedPersonalDataDeletionHandler.Handle(
            Eto(Tenant), cacheEraser, currentTenant, TestContext.Current.CancellationToken));
    }
}
