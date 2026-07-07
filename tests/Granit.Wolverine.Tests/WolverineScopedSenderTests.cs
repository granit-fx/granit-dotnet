// =============================================================================
// Tests - WolverineScopedSender
// =============================================================================
// Verifies that the singleton-friendly sender creates a DI scope per dispatch,
// resolves the scoped IMessageBus, honors cancellation, and records the
// granit.wolverine.messages.dispatched counter tagged with the scope's tenant.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineScopedSenderTests
{
    private sealed record TestCommand(string Payload);

    private static ServiceProvider BuildProvider(IMessageBus bus, ICurrentTenant? tenant = null)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddScoped(_ => bus);
        if (tenant is not null)
        {
            services.AddSingleton(tenant);
        }

        return services.BuildServiceProvider();
    }

    private static WolverineScopedSender CreateSender(ServiceProvider sp) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(),
            new WolverineMetrics(sp.GetRequiredService<IMeterFactory>()));

    private static MetricCollector<long> DispatchedCollector(ServiceProvider sp) =>
        new(sp.GetRequiredService<IMeterFactory>(),
            WolverineMetrics.MeterName,
            "granit.wolverine.messages.dispatched");

    [Fact]
    public async Task SendAsync_resolves_scoped_bus_and_forwards_command()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        await using ServiceProvider sp = BuildProvider(bus);
        WolverineScopedSender sender = CreateSender(sp);
        TestCommand command = new("hello");

        await sender.SendAsync(command, TestContext.Current.CancellationToken);

        await bus.Received(1).SendAsync(command);
    }

    [Fact]
    public async Task SendAsync_throws_when_token_already_canceled()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        await using ServiceProvider sp = BuildProvider(bus);
        WolverineScopedSender sender = CreateSender(sp);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sender.SendAsync(new TestCommand("late"), cts.Token));

        await bus.DidNotReceiveWithAnyArgs().SendAsync<TestCommand>(default!);
    }

    [Fact]
    public async Task SendAsync_records_dispatched_metric_with_global_tenant_when_none_registered()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        await using ServiceProvider sp = BuildProvider(bus);
        WolverineScopedSender sender = CreateSender(sp);
        using MetricCollector<long> collector = DispatchedCollector(sp);

        await sender.SendAsync(new TestCommand("hello"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public async Task SendAsync_records_dispatched_metric_with_scope_tenant()
    {
        var tenantId = Guid.NewGuid();
        IMessageBus bus = Substitute.For<IMessageBus>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(tenantId);
        await using ServiceProvider sp = BuildProvider(bus, tenant);
        WolverineScopedSender sender = CreateSender(sp);
        using MetricCollector<long> collector = DispatchedCollector(sp);

        await sender.SendAsync(new TestCommand("hello"), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.ShouldHaveSingleItem();
        snapshot[0].Tags["tenant_id"].ShouldBe(tenantId.ToString());
    }
}
