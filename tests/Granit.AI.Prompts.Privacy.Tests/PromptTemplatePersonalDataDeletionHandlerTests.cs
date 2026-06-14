using Granit.AI.Prompts.Privacy.DataDeletion;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Xunit;

namespace Granit.AI.Prompts.Privacy.Tests;

public sealed class PromptTemplatePersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_erases_the_subjects_prompts_for_the_event_tenant()
    {
        IPromptTemplateDataManager dataManager = Substitute.For<IPromptTemplateDataManager>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        await PromptTemplatePersonalDataDeletionHandler.Handle(
            Eto(Tenant), dataManager, currentTenant, TestContext.Current.CancellationToken);

        await dataManager.Received(1).EraseOwnerAsync(Tenant, User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        IPromptTemplateDataManager dataManager = Substitute.For<IPromptTemplateDataManager>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(Tenant);

        await PromptTemplatePersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), dataManager, currentTenant, TestContext.Current.CancellationToken);

        await dataManager.Received(1).EraseOwnerAsync(Tenant, User, Arg.Any<CancellationToken>());
    }
}
