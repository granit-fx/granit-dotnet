using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Auditing.Notifications.Handlers;
using Granit.Auditing.Notifications.Options;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Granit.Auditing.Notifications.Tests.Handlers;

public sealed class AnomalyDetectedHandlerTests
{
    [Fact]
    public async Task HandleAsync_AlertableCategory_PublishesNotificationToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IOptions<AuditingNotificationsOptions> options = Microsoft.Extensions.Options.Options.Create(
            new AuditingNotificationsOptions { AlertableCategories = [AuditCategory.AccessDenied] });
        var entryId = Guid.NewGuid();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        AuditEntryPersistedEto evt = new(
            Id: entryId,
            Timestamp: occurredAt,
            UserId: "alice",
            Category: AuditCategory.AccessDenied,
            EntityChangeCount: 0,
            TenantId: tenantId);

        await AnomalyDetectedHandler.HandleAsync(evt, publisher, options, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            AuditingAnomalyDetectedNotificationType.Instance,
            Arg.Is<AuditingAnomalyDetectedNotificationData>(d =>
                d.AuditEntryId == entryId
                && d.Category == "AccessDenied"
                && d.OccurredAt == occurredAt
                && d.ActorUserId == "alice"
                && d.EntityChangeCount == 0
                && d.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonAlertableCategory_DoesNotPublish()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IOptions<AuditingNotificationsOptions> options = Microsoft.Extensions.Options.Options.Create(
            new AuditingNotificationsOptions { AlertableCategories = [AuditCategory.AccessDenied] });
        AuditEntryPersistedEto evt = new(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "alice",
            Category: AuditCategory.DataMutation,
            EntityChangeCount: 1,
            TenantId: null);

        await AnomalyDetectedHandler.HandleAsync(evt, publisher, options, TestContext.Current.CancellationToken);

        await publisher.DidNotReceiveWithAnyArgs().PublishToSubscribersAsync(
            Arg.Any<Granit.Notifications.NotificationType<AuditingAnomalyDetectedNotificationData>>(),
            Arg.Any<AuditingAnomalyDetectedNotificationData>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GlobalScope_TenantIdIsNull()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IOptions<AuditingNotificationsOptions> options = Microsoft.Extensions.Options.Options.Create(
            new AuditingNotificationsOptions { AlertableCategories = [AuditCategory.ConfigurationChange] });
        AuditEntryPersistedEto evt = new(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "platform-admin",
            Category: AuditCategory.ConfigurationChange,
            EntityChangeCount: 3,
            TenantId: null);

        await AnomalyDetectedHandler.HandleAsync(evt, publisher, options, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            AuditingAnomalyDetectedNotificationType.Instance,
            Arg.Is<AuditingAnomalyDetectedNotificationData>(d =>
                d.Category == "ConfigurationChange"
                && d.ActorUserId == "platform-admin"
                && d.EntityChangeCount == 3
                && d.TenantId == null),
            Arg.Any<CancellationToken>());
    }
}
