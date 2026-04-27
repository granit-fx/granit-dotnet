using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.CustomerBalance.Notifications.Tests.Handlers;

public sealed class BalanceDepletedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesBalanceDepletedNotification_ToOwningParty()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        var balanceAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var depletedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        BalanceDepletedEto evt = new(
            BalanceAccountId: balanceAccountId,
            TenantId: tenantId,
            PartyId: partyId,
            Currency: "EUR",
            DepletedAt: depletedAt);

        await BalanceDepletedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            BalanceDepletedNotificationType.Instance,
            Arg.Is<BalanceDepletedNotificationData>(d =>
                d.BalanceAccountId == balanceAccountId &&
                d.TenantId == tenantId &&
                d.PartyId == partyId &&
                d.Currency == "EUR" &&
                d.DepletedAt == depletedAt),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == partyId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
