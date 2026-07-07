// =============================================================================
// Tests - WolverineCommandSender
// =============================================================================
// Verifies that ICommandSender forwards commands to IMessageBus.SendAsync,
// validates its null-argument contract, honors cancellation, and records the
// granit.wolverine.messages.dispatched counter tagged with the current tenant.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Granit.Wolverine.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineCommandSenderTests : IDisposable
{
    private sealed record TestCommand(string Payload);

    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly WolverineMetrics _metrics;

    public WolverineCommandSenderTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WolverineMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    private MetricCollector<long> DispatchedCollector() =>
        new(_meterFactory, WolverineMetrics.MeterName, "granit.wolverine.messages.dispatched");

    [Fact]
    public async Task SendAsync_forwards_command_to_IMessageBus()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus, _metrics, NullTenantContext.Instance);
        TestCommand command = new("hello");

        await sut.SendAsync(command, TestContext.Current.CancellationToken);

        await bus.Received(1).SendAsync(command);
    }

    [Fact]
    public async Task SendAsync_throws_when_command_is_null()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus, _metrics, NullTenantContext.Instance);

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendAsync<TestCommand>(null!, TestContext.Current.CancellationToken));

        await bus.DidNotReceiveWithAnyArgs().SendAsync<TestCommand>(default!);
    }

    [Fact]
    public async Task SendAsync_throws_when_token_already_canceled()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus, _metrics, NullTenantContext.Instance);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.SendAsync(new TestCommand("late"), cts.Token));

        await bus.DidNotReceiveWithAnyArgs().SendAsync<TestCommand>(default!);
    }

    [Fact]
    public async Task SendAsync_records_dispatched_metric_with_global_tenant()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus, _metrics, NullTenantContext.Instance);
        using MetricCollector<long> collector = DispatchedCollector();

        await sut.SendAsync(new TestCommand("hello"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public async Task SendAsync_records_dispatched_metric_with_current_tenant()
    {
        var tenantId = Guid.NewGuid();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(tenantId);
        WolverineCommandSender sut = new(bus, _metrics, tenant);
        using MetricCollector<long> collector = DispatchedCollector();

        await sut.SendAsync(new TestCommand("hello"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe(tenantId.ToString());
    }
}
