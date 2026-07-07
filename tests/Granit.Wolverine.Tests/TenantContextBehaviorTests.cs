// =============================================================================
// Tests - TenantContextBehavior
// =============================================================================
// Verifies that X-Tenant-Id header is correctly restored to ICurrentTenant in
// incoming Wolverine envelopes, that the scope is disposed on After(), that
// envelopes without a tenant header emit an observability metric, and that the
// behavior short-circuits in single-tenant deployments.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Diagnostics;
using Granit.Wolverine.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
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

    private TenantContextBehavior CreateBehavior(ICurrentTenant currentTenant) =>
        new(currentTenant, _metrics);

    private MetricCollector<long> NoTenantCollector() =>
        new(_meterFactory, WolverineMetrics.MeterName, "granit.wolverine.envelope.no_tenant");

    private MetricCollector<long> HandledCollector() =>
        new(_meterFactory, WolverineMetrics.MeterName, "granit.wolverine.messages.handled");

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
    public void Before_WithValidTenantHeader_RecordsMessageHandled()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(tenantId).Returns(Substitute.For<IDisposable>());
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = HandledCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = tenantId.ToString();

        behavior.Before(envelope);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe(tenantId.ToString());
        snapshot[0].Tags["message_type"].ShouldBe(nameof(TenantScopedMessage));
    }

    [Fact]
    public void Before_WithMissingHeader_DoesNotRecordMessageHandled()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = HandledCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };

        behavior.Before(envelope);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Before_WithMissingHeader_RecordsUnmarked()
    {
        // Envelope without tenant header for a regular message: observability only.
        // Authorization is the handler's responsibility (per-(user, tenant) permission check).
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["outcome"].ShouldBe("unmarked");
        snapshot[0].Tags["message_type"].ShouldBe(nameof(TenantScopedMessage));
    }

    [Fact]
    public void Before_WithInvalidGuidHeader_RecordsUnmarked()
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
    public void Before_WithCrossTenantMessage_NoHeader_RecordsMarked()
    {
        // Messages legitimately host-scope opt in via [CrossTenantMessage].
        // Distinct outcome tag so dashboards can suppress them from leak alerts.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = CreateBehavior(tenant);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new SystemBroadcastMessage() };

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["outcome"].ShouldBe("marked");
        snapshot[0].Tags["message_type"].ShouldBe(nameof(SystemBroadcastMessage));
    }

    [Fact]
    public void Before_NonMultiTenantApp_ShortCircuits()
    {
        // Single-tenant / non-MT deployment: ICurrentTenant resolves to
        // NullTenantContext. The behavior must not record observability noise
        // for a signal that does not apply.
        TenantContextBehavior behavior = CreateBehavior(NullTenantContext.Instance);
        using MetricCollector<long> collector = NoTenantCollector();
        Envelope envelope = new() { Message = new TenantScopedMessage() };

        behavior.Before(envelope);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
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
