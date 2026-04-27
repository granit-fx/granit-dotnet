using Granit.Parties.Domain.ValueObjects;
using Granit.Scheduling;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Granit.Subscriptions.Scheduling;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultDunningServiceTests
{
    private readonly ISubscriptionReader _reader = Substitute.For<ISubscriptionReader>();
    private readonly ISubscriptionWriter _writer = Substitute.For<ISubscriptionWriter>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILogger<DefaultDunningService> _logger = NullLoggerFactory.Instance.CreateLogger<DefaultDunningService>();
    private readonly DefaultDunningService _sut;

    public DefaultDunningServiceTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _scheduler.ScheduleAsync(
                Arg.Any<RetryPaymentPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(ScheduledActionId.Create(Guid.NewGuid()));
        _sut = new DefaultDunningService(_reader, _writer, _scheduler, _clock, _logger);
    }

    private static Subscription CreateActiveSubscription(Guid tenantId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));
    }

    // ── HandlePaymentFailureAsync tests ───────────────────────────

    [Fact]
    public async Task HandlePaymentFailureAsync_NoSubscription_ShouldNotWrite()
    {
        var tenantId = Guid.NewGuid();
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await _sut.HandlePaymentFailureAsync(
            tenantId, Guid.NewGuid(), 100m, "EUR", "card", "stripe",
            TestContext.Current.CancellationToken);

        await _writer.DidNotReceive()
            .UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_FirstFailure_ShouldMarkPastDueAndScheduleRetry()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreateActiveSubscription(tenantId);
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);

        await _sut.HandlePaymentFailureAsync(
            tenantId, Guid.NewGuid(), 50m, "EUR", "card", "stripe",
            TestContext.Current.CancellationToken);

        sub.Status.ShouldBe(SubscriptionStatus.PastDue);
        sub.DunningAttempt.ShouldBe(1);
        await _scheduler.Received(1).ScheduleAsync(
            Arg.Any<RetryPaymentPayload>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_ExceedsMaxRetries_ShouldSuspend()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreateActiveSubscription(tenantId);
        sub.MarkPastDue();
        sub.IncrementDunningAttempt(); // 1
        sub.IncrementDunningAttempt(); // 2
        sub.IncrementDunningAttempt(); // 3
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);

        await _sut.HandlePaymentFailureAsync(
            tenantId, Guid.NewGuid(), 50m, "EUR", "card", "stripe",
            TestContext.Current.CancellationToken);

        sub.Status.ShouldBe(SubscriptionStatus.Suspended);
        sub.DunningAttempt.ShouldBe(4);
        await _scheduler.DidNotReceive().ScheduleAsync(
            Arg.Any<RetryPaymentPayload>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_SecondFailure_ShouldScheduleRetryAt7Days()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreateActiveSubscription(tenantId);
        sub.MarkPastDue();
        sub.IncrementDunningAttempt(); // 1
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        DateTimeOffset now = _clock.Now;

        await _sut.HandlePaymentFailureAsync(
            tenantId, Guid.NewGuid(), 50m, "EUR", "card", "stripe",
            TestContext.Current.CancellationToken);

        sub.Status.ShouldBe(SubscriptionStatus.PastDue);
        sub.DunningAttempt.ShouldBe(2);
        await _scheduler.Received(1).ScheduleAsync(
            Arg.Is<RetryPaymentPayload>(p => p.Attempt == 2),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_ExactlyMaxRetries_ShouldStillScheduleRetry()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreateActiveSubscription(tenantId);
        sub.MarkPastDue();
        sub.IncrementDunningAttempt(); // 1
        sub.IncrementDunningAttempt(); // 2
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);

        await _sut.HandlePaymentFailureAsync(
            tenantId, Guid.NewGuid(), 50m, "EUR", "card", "stripe",
            TestContext.Current.CancellationToken);

        sub.DunningAttempt.ShouldBe(3);
        sub.Status.ShouldBe(SubscriptionStatus.PastDue);
        await _scheduler.Received(1).ScheduleAsync(
            Arg.Any<RetryPaymentPayload>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ── CalculateRetryDate tests ──────────────────────────────────

    [Fact]
    public void CalculateRetryDate_Attempt1_ShouldReturn3Days()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTimeOffset result = DefaultDunningService.CalculateRetryDate(now, 1);

        result.ShouldBe(now.AddDays(3));
    }

    [Fact]
    public void CalculateRetryDate_Attempt2_ShouldReturn7Days()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTimeOffset result = DefaultDunningService.CalculateRetryDate(now, 2);

        result.ShouldBe(now.AddDays(7));
    }

    [Fact]
    public void CalculateRetryDate_Attempt3OrMore_ShouldReturn14Days()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTimeOffset result = DefaultDunningService.CalculateRetryDate(now, 3);

        result.ShouldBe(now.AddDays(14));
    }
}
