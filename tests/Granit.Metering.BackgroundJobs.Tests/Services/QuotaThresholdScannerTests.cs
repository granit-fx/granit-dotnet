using Granit.DataFiltering;
using Granit.Metering.BackgroundJobs.Services;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Metering.Events;
using Granit.Metering.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;
using ICurrentTenant = Granit.MultiTenancy.ICurrentTenant;
using IMultiTenant = Granit.Domain.IMultiTenant;

namespace Granit.Metering.BackgroundJobs.Tests.Services;

public sealed class QuotaThresholdScannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IMeterDefinitionReader _definitionReader = Substitute.For<IMeterDefinitionReader>();
    private readonly IQuotaChecker _quotaChecker = Substitute.For<IQuotaChecker>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly GranitMeteringOptions _options = new() { ThresholdPercentage = 80 };
    private readonly QuotaThresholdScanner _sut;

    public QuotaThresholdScannerTests()
    {
        _clock.Now.Returns(Now);
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
        _currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());

        _sut = new QuotaThresholdScanner(
            _definitionReader,
            _quotaChecker,
            _currentTenant,
            _dataFilter,
            _messageBus,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            NullLogger<QuotaThresholdScanner>.Instance);
    }

    // ======== No definitions ========

    [Fact]
    public async Task ScanAsync_NoDefinitions_ShouldNotPublishAnyEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MeterDefinition>());

        await _sut.ScanAsync(ct);

        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<object>());
    }

    // ======== Definition without tenant ========

    [Fact]
    public async Task ScanAsync_DefinitionWithoutTenant_ShouldSkip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MeterDefinition definition = CreateDefinition(tenantId: null);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        await _sut.ScanAsync(ct);

        await _quotaChecker.DidNotReceive()
            .CheckAsync(Arg.Any<Guid>(), Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>());
        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<object>());
    }

    // ======== Quota exceeded ========

    [Fact]
    public async Task ScanAsync_QuotaExceeded_ShouldPublishExceededEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 1200m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<QuotaExceededEto>(e =>
                e.TenantId == tenantId &&
                e.MeterDefinitionId == definition.Id &&
                e.MeterName == "API Calls" &&
                e.CurrentUsage == 1200m &&
                e.Limit == 1000m));
    }

    // ======== Threshold reached (not exceeded) ========

    [Fact]
    public async Task ScanAsync_ThresholdReached_ShouldPublishThresholdEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 850m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<QuotaThresholdReachedEto>(e =>
                e.TenantId == tenantId &&
                e.MeterDefinitionId == definition.Id &&
                e.MeterName == "API Calls" &&
                e.CurrentUsage == 850m &&
                e.Limit == 1000m &&
                e.PercentUsed == 85m));
    }

    // ======== Below threshold ========

    [Fact]
    public async Task ScanAsync_BelowThreshold_ShouldNotPublishAnyEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 500m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<object>());
    }

    // ======== Unlimited meter ========

    [Fact]
    public async Task ScanAsync_UnlimitedMeter_ShouldNotPublishAnyEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.Unlimited("API Calls", 5000m));

        await _sut.ScanAsync(ct);

        await _messageBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<object>());
    }

    // ======== Multiple definitions ========

    [Fact]
    public async Task ScanAsync_MultipleDefinitions_ShouldCheckEach()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        MeterDefinition def1 = CreateDefinition(tenant1);
        MeterDefinition def2 = CreateDefinition(tenant2);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { def1, def2 });

        _quotaChecker.CheckAsync(tenant1, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 1100m, 1000m));
        _quotaChecker.CheckAsync(tenant2, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("Storage", 500m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.Received(1).PublishAsync(Arg.Any<QuotaExceededEto>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<QuotaThresholdReachedEto>());
    }

    // ======== Disables multi-tenant filter ========

    [Fact]
    public async Task ScanAsync_ShouldDisableMultiTenantFilter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MeterDefinition>());

        await _sut.ScanAsync(ct);

        _dataFilter.Received(1).Disable<IMultiTenant>();
    }

    // ======== Switches tenant context ========

    [Fact]
    public async Task ScanAsync_WithTenantDefinition_ShouldSwitchTenantContext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.Unlimited("API Calls", 0m));

        await _sut.ScanAsync(ct);

        _currentTenant.Received(1).Change(tenantId, Arg.Any<string?>());
    }

    // ======== Exactly at threshold boundary ========

    [Fact]
    public async Task ScanAsync_ExactlyAtThreshold_ShouldPublishThresholdEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 800m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.Received(1).PublishAsync(Arg.Any<QuotaThresholdReachedEto>());
    }

    // ======== Exactly at limit (exceeded) ========

    [Fact]
    public async Task ScanAsync_ExactlyAtLimit_ShouldPublishExceededEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        MeterDefinition definition = CreateDefinition(tenantId);

        _definitionReader.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { definition });

        _quotaChecker.CheckAsync(tenantId, Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(QuotaStatus.WithLimit("API Calls", 1000m, 1000m));

        await _sut.ScanAsync(ct);

        await _messageBus.Received(1).PublishAsync(Arg.Any<QuotaExceededEto>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<QuotaThresholdReachedEto>());
    }

    // ======== Helpers ========

    private static MeterDefinition CreateDefinition(Guid? tenantId)
    {
        var definition = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum);

        if (tenantId.HasValue)
        {
            // Set TenantId via the explicit interface implementation.
            ((IMultiTenant)definition).TenantId = tenantId.Value;
        }

        return definition;
    }
}
