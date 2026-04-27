using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultSubscriptionProviderSyncServiceTests
{
    private readonly ISubscriptionReader _subscriptionReader = Substitute.For<ISubscriptionReader>();
    private readonly ISubscriptionProvider _provider = Substitute.For<ISubscriptionProvider>();
    private readonly ILogger<DefaultSubscriptionProviderSyncService> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultSubscriptionProviderSyncService>();
    private readonly DefaultSubscriptionProviderSyncService _sut;

    public DefaultSubscriptionProviderSyncServiceTests()
    {
        _provider.Name.Returns("stripe");
        _sut = new DefaultSubscriptionProviderSyncService(_subscriptionReader, _provider, _logger);
    }

    private static Subscription CreateSubscription(Guid tenantId, PlanId planId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            PartyId.Create(Guid.NewGuid()),
            planId,
            currency: "EUR",
            new SubscriptionPeriod(now, now.AddMonths(1), BillingCycleAnchor: now));
    }

    // ======== SubscriptionNotFound ========

    [Fact]
    public async Task SyncCancellationAsync_SubscriptionNotFound_ShouldReturnWithoutCallingProvider()
    {
        var subscriptionId = Guid.NewGuid();
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await _sut.SyncCancellationAsync(
            subscriptionId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        await _provider.DidNotReceive()
            .CancelExternalAsync(Arg.Any<SubscriptionExternalMapping>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ======== Tenant mismatch ========

    [Fact]
    public async Task SyncCancellationAsync_TenantMismatch_ShouldReturnWithoutCallingProvider()
    {
        var realTenantId = Guid.NewGuid();
        var wrongTenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateSubscription(realTenantId, planId);

        _subscriptionReader.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _sut.SyncCancellationAsync(
            subscription.Id, wrongTenantId, TestContext.Current.CancellationToken);

        await _provider.DidNotReceive()
            .CancelExternalAsync(Arg.Any<SubscriptionExternalMapping>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ======== No external mapping ========

    [Fact]
    public async Task SyncCancellationAsync_NoExternalMapping_ShouldNotCallProvider()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateSubscription(tenantId, planId);

        _subscriptionReader.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _sut.SyncCancellationAsync(
            subscription.Id, tenantId, TestContext.Current.CancellationToken);

        await _provider.DidNotReceive()
            .CancelExternalAsync(Arg.Any<SubscriptionExternalMapping>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ======== With matching external mapping ========

    [Fact]
    public async Task SyncCancellationAsync_WithMatchingMapping_ShouldCallCancelExternalAsync()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateSubscription(tenantId, planId);

        var mapping = SubscriptionExternalMapping.Create(Guid.NewGuid(), "stripe", "sub_abc123");
        subscription.AddExternalMapping(mapping);

        _subscriptionReader.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _sut.SyncCancellationAsync(
            subscription.Id, tenantId, TestContext.Current.CancellationToken);

        await _provider.Received(1).CancelExternalAsync(
            mapping, atPeriodEnd: false, Arg.Any<CancellationToken>());
    }

    // ======== Non-matching provider mapping ========

    [Fact]
    public async Task SyncCancellationAsync_WithNonMatchingProviderMapping_ShouldNotCallProvider()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateSubscription(tenantId, planId);

        var mapping = SubscriptionExternalMapping.Create(Guid.NewGuid(), "mollie", "tr_xyz789");
        subscription.AddExternalMapping(mapping);

        _subscriptionReader.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _sut.SyncCancellationAsync(
            subscription.Id, tenantId, TestContext.Current.CancellationToken);

        await _provider.DidNotReceive()
            .CancelExternalAsync(Arg.Any<SubscriptionExternalMapping>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ======== Multiple mappings — only matching provider called ========

    [Fact]
    public async Task SyncCancellationAsync_MultipleMappings_ShouldOnlyCallMatchingProvider()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateSubscription(tenantId, planId);

        var mollieMapping = SubscriptionExternalMapping.Create(Guid.NewGuid(), "mollie", "tr_xyz");
        var stripeMapping = SubscriptionExternalMapping.Create(Guid.NewGuid(), "stripe", "sub_abc");
        subscription.AddExternalMapping(mollieMapping);
        subscription.AddExternalMapping(stripeMapping);

        _subscriptionReader.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _sut.SyncCancellationAsync(
            subscription.Id, tenantId, TestContext.Current.CancellationToken);

        await _provider.Received(1).CancelExternalAsync(
            stripeMapping, atPeriodEnd: false, Arg.Any<CancellationToken>());
    }
}
