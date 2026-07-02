using Granit.AI.Chat.Privacy.DataDeletion;
using Granit.AI.Chat.Privacy.DataExport;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AI.Chat.Privacy.Tests;

public sealed class ConversationPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_erases_the_subjects_conversations_for_the_event_tenant()
    {
        IConversationDataStore dataManager = Substitute.For<IConversationDataStore>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        await ConversationPersonalDataDeletionHandler.Handle(
            Eto(Tenant), dataManager, currentTenant, TestContext.Current.CancellationToken);

        await dataManager.Received(1).EraseOwnerAsync(Tenant, User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        IConversationDataStore dataManager = Substitute.For<IConversationDataStore>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(Tenant);

        await ConversationPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), dataManager, currentTenant, TestContext.Current.CancellationToken);

        await dataManager.Received(1).EraseOwnerAsync(Tenant, User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_acknowledges_with_the_registered_provider_name_and_erased_count()
    {
        IConversationDataStore dataManager = Substitute.For<IConversationDataStore>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        dataManager.EraseOwnerAsync(Tenant, User, Arg.Any<CancellationToken>()).Returns(7);
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);

        PersonalDataDeletedEto ack = await ConversationPersonalDataDeletionHandler.Handle(
            eto, dataManager, currentTenant, TestContext.Current.CancellationToken);

        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(ConversationPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.PhysicalDelete);
        ack.AffectedRecords.ShouldBe(7);
        ack.TenantId.ShouldBe(Tenant);
    }

    [Fact]
    public async Task Handle_does_not_acknowledge_when_the_erasure_fails()
    {
        IConversationDataStore dataManager = Substitute.For<IConversationDataStore>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        dataManager.EraseOwnerAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("conversation erase failed"));

        await Should.ThrowAsync<InvalidOperationException>(() => ConversationPersonalDataDeletionHandler.Handle(
            Eto(Tenant), dataManager, currentTenant, TestContext.Current.CancellationToken));
    }
}
