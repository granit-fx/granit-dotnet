using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.CustomerBalance.Notifications.Tests.Handlers;

public sealed class CreditGrantedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesCreditGrantedNotification_ToOwningParty()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var balanceAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        BalanceCreditedEto evt = new(
            BalanceAccountId: balanceAccountId,
            TenantId: tenantId,
            PartyId: partyId,
            Amount: 25.00m,
            Currency: "EUR",
            Source: TransactionSource.Promotional);

        await CreditGrantedNotificationHandler.HandleAsync(
            evt,
            publisher,
            CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            CreditGrantedNotificationType.Instance,
            Arg.Is<CreditGrantedNotificationData>(d =>
                d.BalanceAccountId == balanceAccountId &&
                d.TenantId == tenantId &&
                d.PartyId == partyId &&
                d.Amount == 25.00m &&
                d.Currency == "EUR" &&
                d.Source == "Promotional"),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == partyId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
