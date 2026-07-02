using Granit.Identity.Local.Privacy.DataDeletion;
using Granit.Identity.Local.Privacy.DataExport;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Privacy.Tests.DataDeletion;

public sealed class IdentityLocalPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_initiates_account_deletion_for_the_events_user()
    {
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

        await IdentityLocalPersonalDataDeletionHandler.Handle(
            Eto(Tenant), deletionService, currentTenant, TestContext.Current.CancellationToken);

        await deletionService.Received(1).InitiateAsync(User.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_activates_the_event_tenant_before_deleting()
    {
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

        await IdentityLocalPersonalDataDeletionHandler.Handle(
            Eto(Tenant), deletionService, currentTenant, TestContext.Current.CancellationToken);

        currentTenant.Received(1).Change(Tenant);
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(Tenant);
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

        await IdentityLocalPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), deletionService, currentTenant, TestContext.Current.CancellationToken);

        currentTenant.Received(1).Change(Tenant);
        await deletionService.Received(1).InitiateAsync(User.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_acknowledges_with_the_registered_provider_name_and_request_id()
    {
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);

        PersonalDataDeletedEto ack = await IdentityLocalPersonalDataDeletionHandler.Handle(
            eto, deletionService, currentTenant, TestContext.Current.CancellationToken);

        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(IdentityLocalPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.SoftDelete);
        ack.TenantId.ShouldBe(Tenant);
    }

    [Fact]
    public async Task Handle_does_not_acknowledge_when_the_erasure_fails()
    {
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());
        deletionService.InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("account deletion failed"));

        // The exception propagates so Wolverine retries / dead-letters; no ack is produced,
        // so the saga correctly keeps this provider pending → PartiallyExecuted on timeout.
        await Should.ThrowAsync<InvalidOperationException>(() => IdentityLocalPersonalDataDeletionHandler.Handle(
            Eto(Tenant), deletionService, currentTenant, TestContext.Current.CancellationToken));
    }
}
