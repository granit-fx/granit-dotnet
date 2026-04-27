using Granit.Metering.Events;
using Granit.Metering.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Metering.Notifications.Tests.Handlers;

public sealed class QuotaThresholdReachedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesThresholdReachedNotificationToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var tenantId = Guid.NewGuid();
        var meterDefinitionId = Guid.NewGuid();
        QuotaThresholdReachedEto evt = new(
            TenantId: tenantId,
            MeterDefinitionId: meterDefinitionId,
            MeterName: "api_calls",
            CurrentUsage: 8_400m,
            Limit: 10_000m,
            PercentUsed: 84m);

        await QuotaThresholdReachedNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            MeteringQuotaThresholdReachedNotificationType.Instance,
            Arg.Is<MeteringQuotaThresholdReachedNotificationData>(d =>
                d.TenantId == tenantId &&
                d.MeterDefinitionId == meterDefinitionId &&
                d.MeterName == "api_calls" &&
                d.CurrentUsage == 8_400m &&
                d.Limit == 10_000m &&
                d.PercentUsed == 84m),
            Arg.Any<CancellationToken>());
    }
}
