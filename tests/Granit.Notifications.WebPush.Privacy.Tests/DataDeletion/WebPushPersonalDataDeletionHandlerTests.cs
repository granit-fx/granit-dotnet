using Granit.MultiTenancy;
using Granit.Notifications.WebPush.Privacy.DataDeletion;
using Granit.Notifications.WebPush.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Privacy.Tests.DataDeletion;

public sealed class WebPushPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_erases_the_subjects_subscriptions_and_reports_the_real_count()
    {
        IWebPushSubscriptionWriter writer = Substitute.For<IWebPushSubscriptionWriter>();
        writer.EraseUserDataAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>()).Returns(2);
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        PersonalDataDeletedEto ack = await WebPushPersonalDataDeletionHandler.Handle(
            Eto(Tenant), writer, currentTenant, TestContext.Current.CancellationToken);

        ack.ProviderName.ShouldBe(WebPushPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.PhysicalDelete);
        ack.AffectedRecords.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        IWebPushSubscriptionWriter writer = Substitute.For<IWebPushSubscriptionWriter>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(Tenant);

        await WebPushPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), writer, currentTenant, TestContext.Current.CancellationToken);

        await writer.Received(1).EraseUserDataAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_does_not_acknowledge_when_the_erasure_fails()
    {
        IWebPushSubscriptionWriter writer = Substitute.For<IWebPushSubscriptionWriter>();
        writer.EraseUserDataAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("erase failed"));

        await Should.ThrowAsync<InvalidOperationException>(() => WebPushPersonalDataDeletionHandler.Handle(
            Eto(Tenant), writer, Substitute.For<ICurrentTenant>(), TestContext.Current.CancellationToken));
    }
}
