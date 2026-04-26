using Granit.Contacts.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultSubscriptionReactivationServiceTests
{
    private readonly ISubscriptionReader _reader = Substitute.For<ISubscriptionReader>();
    private readonly ISubscriptionWriter _writer = Substitute.For<ISubscriptionWriter>();
    private readonly ILogger<DefaultSubscriptionReactivationService> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultSubscriptionReactivationService>();
    private readonly DefaultSubscriptionReactivationService _sut;

    public DefaultSubscriptionReactivationServiceTests()
    {
        _sut = new DefaultSubscriptionReactivationService(_reader, _writer, _logger);
    }

    private static Subscription CreatePastDueSubscription(Guid tenantId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var sub = Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            ContactId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));
        sub.MarkPastDue();
        sub.IncrementDunningAttempt();
        return sub;
    }

    // ── TryReactivateAsync tests ──────────────────────────────────

    [Fact]
    public async Task TryReactivateAsync_NoSubscription_ShouldReturnFalse()
    {
        var tenantId = Guid.NewGuid();
        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        bool result = await _sut.TryReactivateAsync(tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryReactivateAsync_ActiveSubscription_ShouldReturnFalse()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var sub = Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            ContactId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()),
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));

        _reader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);

        bool result = await _sut.TryReactivateAsync(tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryReactivateAsync_PastDueSubscription_ShouldActivateAndResetDunning()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = CreatePastDueSubscription(tenantId);
        _reader.GetPastDueForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);

        bool result = await _sut.TryReactivateAsync(tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Active);
        sub.DunningAttempt.ShouldBe(0);
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }
}
