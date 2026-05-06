// =============================================================================
// Tests - TenantContextBehavior
// =============================================================================
// Verifies that X-Tenant-Id header is correctly restored to ICurrentTenant in
// incoming Wolverine envelopes, that the scope is disposed on After(), and
// that envelopes without a tenant header are observable / fail-closed when
// requested.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Diagnostics;
using Granit.Wolverine.Middleware;
using Granit.Wolverine.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class TenantContextBehaviorTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly WolverineMetrics _metrics;

    public TenantContextBehaviorTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WolverineMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    private TenantContextBehavior CreateBehavior(
        ICurrentTenant currentTenant,
        bool requireEnvelopeTenant = false)
    {
        IOptions<WolverineMessagingOptions> options = Microsoft.Extensions.Options.Options.Create(
            new WolverineMessagingOptions { RequireEnvelopeTenant = requireEnvelopeTenant });
        return new TenantContextBehavior(currentTenant, _metrics, options);
    }

    private MetricCollector<long> NoTenantCollector() =>
        new(_meterFactory, WolverineMetrics.MeterName, "granit.wolverine.envelope.no_tenant");

    [Fact]
    public void Before_WithValidTenantHeader_CallsChange()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(tenantId).Returns(Substitute.For<IDisposable>());
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = tenantId.ToString();

        behavior.Before(envelope);

        tenant.Received(1).Change(tenantId);
        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Before_WithMissingHeader_PassesThrough_AndRecordsMetric()
    {
        // Default behavior is permissive: handler runs without scope. The metric
        // makes the occurrence visible so SOC can quantify the migration backlog
        // before flipping RequireEnvelopeTenant.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["outcome"].ShouldBe("allowed");
        snapshot[0].Tags["message_type"].ShouldBe(nameof(TenantScopedMessage));
    }

    [Fact]
    public void Before_WithInvalidGuidHeader_PassesThrough_AndRecordsMetric()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = "not-a-guid";

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
        collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
    }

    [Fact]
    public void Before_WithCrossTenantMessage_NoHeader_AllowedMarked()
    {
        // System-wide messages opt out of the gate via [CrossTenantMessage].
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant, requireEnvelopeTenant: true);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new SystemBroadcastMessage() };

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["outcome"].ShouldBe("allowed_marked");
        snapshot[0].Tags["message_type"].ShouldBe(nameof(SystemBroadcastMessage));
    }

    [Fact]
    public void Before_WhenRequireEnvelopeTenant_NoHeader_NotMarked_Throws()
    {
        // With the gate enabled, untenanted envelopes for tenant-scoped messages
        // must throw — they signal a producer-side bug or an envelope forgery.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant, requireEnvelopeTenant: true);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };

        Should.Throw<InvalidOperationException>(() => behavior.Before(envelope))
            .Message.ShouldContain(nameof(CrossTenantMessageAttribute));
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["outcome"].ShouldBe("rejected");
    }

    [Fact]
    public void After_WhenScopeWasSet_DisposesScope()
    {
        var tenantId = Guid.NewGuid();
        IDisposable scope = Substitute.For<IDisposable>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(tenantId).Returns(scope);
        TenantContextBehavior behavior = CreateBehavior(tenant);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = tenantId.ToString();

        behavior.Before(envelope);
        behavior.After();

        scope.Received(1).Dispose();
    }

    [Fact]
    public void After_WhenNoScopeWasSet_DoesNotThrow()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);

        Should.NotThrow(behavior.After);
    }

    // ──── Test message types ────

    private sealed record TenantScopedMessage;

    [CrossTenantMessage]
    private sealed record SystemBroadcastMessage;
}
