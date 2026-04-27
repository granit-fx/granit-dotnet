using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.CustomerBalance.Notifications.Tests.Handlers;

public sealed class CreditExpiredNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesCreditExpiredNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var balanceAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        CreditExpiredEto evt = new(
            BalanceAccountId: balanceAccountId,
            TenantId: tenantId,
            Amount: 12.50m,
            Currency: "EUR");

        await CreditExpiredNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            CreditExpiredNotificationType.Instance,
            Arg.Is<CreditExpiredNotificationData>(d =>
                d.BalanceAccountId == balanceAccountId &&
                d.TenantId == tenantId &&
                d.Amount == 12.50m &&
                d.Currency == "EUR"),
            Arg.Any<CancellationToken>());
    }
}
