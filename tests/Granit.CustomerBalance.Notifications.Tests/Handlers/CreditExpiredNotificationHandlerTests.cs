using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.CustomerBalance.Notifications.Tests.Handlers;

public sealed class CreditExpiredNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesCreditExpiredNotification_ToOwningParty()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var balanceAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        CreditExpiredEto evt = new(
            BalanceAccountId: balanceAccountId,
            TenantId: tenantId,
            PartyId: partyId,
            Amount: 12.50m,
            Currency: "EUR");

        await CreditExpiredNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            CreditExpiredNotificationType.Instance,
            Arg.Is<CreditExpiredNotificationData>(d =>
                d.BalanceAccountId == balanceAccountId &&
                d.TenantId == tenantId &&
                d.PartyId == partyId &&
                d.Amount == 12.50m &&
                d.Currency == "EUR"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == partyId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
