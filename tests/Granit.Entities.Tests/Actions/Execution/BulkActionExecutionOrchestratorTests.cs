using System.Text.Json;
using Granit.Domain;
using Granit.Entities.Actions.Execution;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests.Actions.Execution;

public sealed class BulkActionExecutionOrchestratorTests
{
    private sealed class Widget : IEmitEntityLifecycleEvents
    {
        public Guid Id { get; init; }
    }

    private sealed class Gadget
    {
        public Guid Id { get; init; }
    }

    private sealed class PerRowExecutor(Dictionary<Guid, EntityActionExecutionResult<Widget>> outcomes)
        : IEntityActionExecutor<Widget>
    {
        public List<Guid> Calls { get; } = [];

        public Task<EntityActionExecutionResult<Widget>> ExecuteAsync(
            Guid id, JsonElement payload, CancellationToken cancellationToken)
        {
            Calls.Add(id);
            return Task.FromResult(outcomes[id]);
        }
    }

    private sealed class BulkExecutor : IBulkActionExecutor<Widget>
    {
        public IReadOnlyList<Guid>? CapturedIds { get; private set; }

        public Task<BulkActionExecutionResult<Widget>> ExecuteBulkAsync(
            IReadOnlyList<Guid> ids, JsonElement payload, CancellationToken cancellationToken)
        {
            CapturedIds = ids;
            return Task.FromResult(new BulkActionExecutionResult<Widget>(
                AffectedEntities: [.. ids.Select(i => new Widget { Id = i })],
                Failures: []));
        }
    }

    private sealed class GadgetExecutor : IEntityActionExecutor<Gadget>
    {
        public Task<EntityActionExecutionResult<Gadget>> ExecuteAsync(
            Guid id, JsonElement payload, CancellationToken cancellationToken) =>
            Task.FromResult(new EntityActionExecutionResult<Gadget>(new Gadget { Id = id }, FailureReason: null));
    }

    private sealed class RecordingBus : ILocalEventBus
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Published.Add(localEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PerRow_loop_aggregates_successes_and_failures()
    {
        var ok1 = Guid.NewGuid();
        var ok2 = Guid.NewGuid();
        var bad = Guid.NewGuid();

        var e1 = new Widget { Id = ok1 };
        var e2 = new Widget { Id = ok2 };

        var executor = new PerRowExecutor(new()
        {
            [ok1] = new EntityActionExecutionResult<Widget>(e1, null),
            [ok2] = new EntityActionExecutionResult<Widget>(e2, null),
            [bad] = new EntityActionExecutionResult<Widget>(null, "NotFound"),
        });
        RecordingBus bus = new();

        ServiceCollection services = new();
        services.AddSingleton(executor);
        services.AddSingleton<ILocalEventBus>(bus);
        await using ServiceProvider sp = services.BuildServiceProvider();

        BulkActionExecutionOrchestrator orchestrator = new(NullLogger<BulkActionExecutionOrchestrator>.Instance);
        BulkActionDispatchResult result = await orchestrator.DispatchAsync(
            typeof(Widget), typeof(PerRowExecutor),
            [ok1, ok2, bad],
            default,
            sp,
            CancellationToken.None);

        result.Affected.ShouldBe(2);
        BulkActionFailure failure = result.Failures.ShouldHaveSingleItem();
        failure.Id.ShouldBe(bad);
        failure.Reason.ShouldBe("NotFound");
        executor.Calls.Count.ShouldBe(3);

        // Lifecycle bus saw exactly one batched event.
        bus.Published.Count.ShouldBe(1);
        EntityBulkUpdatedEvent<Widget> evt = bus.Published[0].ShouldBeOfType<EntityBulkUpdatedEvent<Widget>>();
        evt.Entities.Select(x => x.Id).ShouldBe([ok1, ok2]);
    }

    [Fact]
    public async Task Prefers_bulk_executor_when_registered()
    {
        BulkExecutor bulk = new();
        PerRowExecutor perRow = new(new());

        ServiceCollection services = new();
        services.AddSingleton<IBulkActionExecutor<Widget>>(bulk);
        services.AddSingleton(perRow);
        await using ServiceProvider sp = services.BuildServiceProvider();

        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid()];
        BulkActionExecutionOrchestrator orchestrator = new(NullLogger<BulkActionExecutionOrchestrator>.Instance);

        BulkActionDispatchResult result = await orchestrator.DispatchAsync(
            typeof(Widget), typeof(PerRowExecutor), ids, default, sp, CancellationToken.None);

        result.Affected.ShouldBe(2);
        result.Failures.ShouldBeEmpty();
        bulk.CapturedIds.ShouldBe(ids);
        perRow.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Skips_lifecycle_event_when_entity_does_not_emit_lifecycle_events()
    {
        GadgetExecutor executor = new();
        RecordingBus bus = new();

        ServiceCollection services = new();
        services.AddSingleton(executor);
        services.AddSingleton<ILocalEventBus>(bus);
        await using ServiceProvider sp = services.BuildServiceProvider();

        BulkActionExecutionOrchestrator orchestrator = new(NullLogger<BulkActionExecutionOrchestrator>.Instance);
        BulkActionDispatchResult result = await orchestrator.DispatchAsync(
            typeof(Gadget), typeof(GadgetExecutor),
            [Guid.NewGuid()], default, sp, CancellationToken.None);

        result.Affected.ShouldBe(1);
        bus.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Skips_event_when_no_lifecycle_bus_registered()
    {
        Widget e = new() { Id = Guid.NewGuid() };
        PerRowExecutor executor = new(new()
        {
            [e.Id] = new EntityActionExecutionResult<Widget>(e, null),
        });

        ServiceCollection services = new();
        services.AddSingleton(executor);
        await using ServiceProvider sp = services.BuildServiceProvider();

        BulkActionExecutionOrchestrator orchestrator = new(NullLogger<BulkActionExecutionOrchestrator>.Instance);
        // No throw, no event — just a successful result.
        BulkActionDispatchResult result = await orchestrator.DispatchAsync(
            typeof(Widget), typeof(PerRowExecutor), [e.Id], default, sp, CancellationToken.None);

        result.Affected.ShouldBe(1);
    }
}
