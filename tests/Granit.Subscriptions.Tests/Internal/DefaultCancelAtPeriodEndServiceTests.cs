using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultCancelAtPeriodEndServiceTests
{
    private readonly ISubscriptionReader _reader = Substitute.For<ISubscriptionReader>();
    private readonly ISubscriptionWriter _writer = Substitute.For<ISubscriptionWriter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly ILogger<DefaultCancelAtPeriodEndService> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultCancelAtPeriodEndService>();
    private readonly DefaultCancelAtPeriodEndService _sut;

    public DefaultCancelAtPeriodEndServiceTests()
    {
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
        _currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);
        _sut = new DefaultCancelAtPeriodEndService(_reader, _writer, _clock, _currentTenant, _dataFilter, _logger);
    }

    private static Subscription CreatePendingCancelSubscription(Guid tenantId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var sub = Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now.AddMonths(-1), now, BillingCycleAnchor: now.AddMonths(-1)));
        sub.ScheduleCancelAtPeriodEnd();
        return sub;
    }

    // ── CancelDueSubscriptionsAsync tests ─────────────────────────

    [Fact]
    public async Task CancelDueSubscriptionsAsync_NoPending_ShouldNotWrite()
    {
        _reader.GetPendingCancelAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Subscription>());

        await _sut.CancelDueSubscriptionsAsync(TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelDueSubscriptionsAsync_WithPendingSubscription_ShouldCancelAndPersist()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreatePendingCancelSubscription(tenantId);
        _reader.GetPendingCancelAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub]);

        await _sut.CancelDueSubscriptionsAsync(TestContext.Current.CancellationToken);

        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);
        sub.CancellationReason.ShouldBe("Scheduled cancel at period end");
        sub.CancelAtPeriodEnd.ShouldBeFalse();
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelDueSubscriptionsAsync_MultipleSubscriptions_ShouldCancelAll()
    {
        Subscription sub1 = CreatePendingCancelSubscription(Guid.NewGuid());
        Subscription sub2 = CreatePendingCancelSubscription(Guid.NewGuid());
        _reader.GetPendingCancelAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub1, sub2]);

        await _sut.CancelDueSubscriptionsAsync(TestContext.Current.CancellationToken);

        sub1.Status.ShouldBe(SubscriptionStatus.Cancelled);
        sub2.Status.ShouldBe(SubscriptionStatus.Cancelled);
        await _writer.Received(2).UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelDueSubscriptionsAsync_ExceptionOnOne_ShouldContinueWithOthers()
    {
        Subscription sub1 = CreatePendingCancelSubscription(Guid.NewGuid());
        Subscription sub2 = CreatePendingCancelSubscription(Guid.NewGuid());
        _reader.GetPendingCancelAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub1, sub2]);

        _writer.UpdateAsync(sub1, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB error")));

        await _sut.CancelDueSubscriptionsAsync(TestContext.Current.CancellationToken);

        sub2.Status.ShouldBe(SubscriptionStatus.Cancelled);
        await _writer.Received(1).UpdateAsync(sub2, Arg.Any<CancellationToken>());
    }
}
