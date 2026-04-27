using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using Granit.Timing;
using NSubstitute;
using Xunit;

namespace Granit.CustomerBalance.Notifications.Tests.Handlers;

public sealed class CreditExpiringNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesCreditExpiringNotification_ToOwningParty_WithDayCountdown()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IClock clock = Substitute.For<IClock>();

        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        clock.Now.Returns(now);

        var balanceAccountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        DateTimeOffset expiresAt = now.AddDays(5);

        CreditExpiringEto evt = new(
            BalanceAccountId: balanceAccountId,
            TenantId: tenantId,
            PartyId: partyId,
            CreditId: creditId,
            Amount: 25.00m,
            Currency: "EUR",
            ExpiresAt: expiresAt);

        await CreditExpiringNotificationHandler.HandleAsync(evt, publisher, clock, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            CreditExpiringNotificationType.Instance,
            Arg.Is<CreditExpiringNotificationData>(d =>
                d.BalanceAccountId == balanceAccountId &&
                d.TenantId == tenantId &&
                d.PartyId == partyId &&
                d.CreditId == creditId &&
                d.Amount == 25.00m &&
                d.Currency == "EUR" &&
                d.ExpiresAt == expiresAt &&
                d.DaysUntilExpiry == 5),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == partyId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
