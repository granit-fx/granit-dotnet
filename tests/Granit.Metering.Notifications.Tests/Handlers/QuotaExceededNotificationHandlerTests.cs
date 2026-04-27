using Granit.Metering.Events;
using Granit.Metering.Notifications.Handlers;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Metering.Notifications.Tests.Handlers;

public sealed class QuotaExceededNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesExceededNotificationToSubscribers_WithDerivedPercentUsed()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var tenantId = Guid.NewGuid();
        var meterDefinitionId = Guid.NewGuid();
        QuotaExceededEto evt = new(
            TenantId: tenantId,
            MeterDefinitionId: meterDefinitionId,
            MeterName: "api_calls",
            CurrentUsage: 12_500m,
            Limit: 10_000m);

        await QuotaExceededNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            MeteringQuotaExceededNotificationType.Instance,
            Arg.Is<MeteringQuotaExceededNotificationData>(d =>
                d.TenantId == tenantId &&
                d.MeterDefinitionId == meterDefinitionId &&
                d.MeterName == "api_calls" &&
                d.CurrentUsage == 12_500m &&
                d.Limit == 10_000m &&
                d.PercentUsed == 125m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithZeroLimit_FallsBackToHundredPercent()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        QuotaExceededEto evt = new(
            TenantId: Guid.NewGuid(),
            MeterDefinitionId: Guid.NewGuid(),
            MeterName: "api_calls",
            CurrentUsage: 42m,
            Limit: 0m);

        await QuotaExceededNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            MeteringQuotaExceededNotificationType.Instance,
            Arg.Is<MeteringQuotaExceededNotificationData>(d => d.PercentUsed == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NotificationType_DefaultsAreEmailAndInAppWithErrorSeverity()
    {
        MeteringQuotaExceededNotificationType.Instance.Name
            .ShouldBe("metering.quota_exceeded");
        MeteringQuotaExceededNotificationType.Instance.DefaultSeverity
            .ShouldBe(NotificationSeverity.Error);
        MeteringQuotaExceededNotificationType.Instance.DefaultChannels.ShouldBe(
            [NotificationChannels.Email, NotificationChannels.InApp]);
    }
}
