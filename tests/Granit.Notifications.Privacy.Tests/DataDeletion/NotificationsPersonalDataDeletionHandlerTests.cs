using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Xunit;

namespace Granit.Notifications.Privacy.Tests.DataDeletion;

public sealed class NotificationsPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task Handle_erases_the_subjects_notification_data_for_the_event_tenant()
    {
        INotificationsPersonalDataEraser eraser = Substitute.For<INotificationsPersonalDataEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();

        await NotificationsPersonalDataDeletionHandler.Handle(
            Eto(Tenant), eraser, currentTenant, TestContext.Current.CancellationToken);

        await eraser.Received(1).EraseUserDataAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_falls_back_to_the_ambient_tenant_when_the_event_has_none()
    {
        INotificationsPersonalDataEraser eraser = Substitute.For<INotificationsPersonalDataEraser>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(Tenant);

        await NotificationsPersonalDataDeletionHandler.Handle(
            Eto(tenantId: null), eraser, currentTenant, TestContext.Current.CancellationToken);

        await eraser.Received(1).EraseUserDataAsync(User.ToString(), Tenant, Arg.Any<CancellationToken>());
    }
}
