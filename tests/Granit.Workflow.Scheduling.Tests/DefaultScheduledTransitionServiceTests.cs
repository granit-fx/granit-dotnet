using Granit.Scheduling;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Workflow.Scheduling.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Scheduling.Tests;

public sealed class DefaultScheduledTransitionServiceTests
{
    private const string EntityType = "BlogPost";
    private static readonly Guid EntityId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly DateTimeOffset ExecuteAt = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IScheduledActionReader _reader = Substitute.For<IScheduledActionReader>();
    private readonly DefaultScheduledTransitionService _sut;

    public DefaultScheduledTransitionServiceTests()
    {
        _scheduler.ScheduleAsync(
                Arg.Any<ScheduledWorkflowTransitionPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(ScheduledActionId.Create(Guid.NewGuid()));

        _sut = new DefaultScheduledTransitionService(_scheduler, _reader);
    }

    [Fact]
    public void BuildCorrelationId_uses_deterministic_workflow_prefix() =>
        DefaultScheduledTransitionService.BuildCorrelationId(EntityType, EntityId)
            .ShouldBe($"workflow:{EntityType}:{EntityId}");

    [Fact]
    public async Task ScheduleTransitionAsync_schedules_payload_with_deterministic_correlation()
    {
        await _sut.ScheduleTransitionAsync(EntityType, EntityId, nameof(TestState.Published), ExecuteAt, Ct);

        string expectedCorrelation = $"workflow:{EntityType}:{EntityId}";
        await _scheduler.Received(1).ScheduleAsync(
            Arg.Is<ScheduledWorkflowTransitionPayload>(p =>
                p.WorkflowEntityType == EntityType
                && p.EntityId == EntityId
                && p.TargetState == nameof(TestState.Published)
                && p.CorrelationId == expectedCorrelation),
            ExecuteAt,
            expectedCorrelation,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScheduleTransitionAsync_rejects_blank_entity_type(string entityType) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.ScheduleTransitionAsync(entityType, EntityId, nameof(TestState.Published), ExecuteAt, Ct));

    [Fact]
    public async Task ScheduleTransitionAsync_rejects_blank_target_state() =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.ScheduleTransitionAsync(EntityType, EntityId, "  ", ExecuteAt, Ct));

    [Fact]
    public async Task CancelScheduledTransitionAsync_cancels_only_pending_actions()
    {
        string correlation = $"workflow:{EntityType}:{EntityId}";
        var pendingId = Guid.NewGuid();

        var pending = ScheduledAction.Create(pendingId, "type", "{}", ExecuteAt, correlation);
        var cancelled = ScheduledAction.Create(Guid.NewGuid(), "type", "{}", ExecuteAt, correlation);
        cancelled.Cancel(cancelledBy: "someone"); // now non-pending

        _reader.GetByCorrelationIdAsync(correlation, Arg.Any<CancellationToken>())
            .Returns([pending, cancelled]);

        await _sut.CancelScheduledTransitionAsync(EntityType, EntityId, Ct);

        await _scheduler.Received(1).CancelAsync(
            Arg.Is<ScheduledActionId>(id => id.Value == pendingId), Arg.Any<CancellationToken>());
        await _scheduler.Received(1).CancelAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelScheduledTransitionAsync_is_noop_when_none_pending()
    {
        _reader.GetByCorrelationIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.CancelScheduledTransitionAsync(EntityType, EntityId, Ct);

        await _scheduler.DidNotReceive().CancelAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RescheduleTransitionAsync_cancels_pending_then_schedules_fresh()
    {
        string correlation = $"workflow:{EntityType}:{EntityId}";
        var pendingId = Guid.NewGuid();
        var pending = ScheduledAction.Create(pendingId, "type", "{}", ExecuteAt, correlation);

        _reader.GetByCorrelationIdAsync(correlation, Arg.Any<CancellationToken>())
            .Returns([pending]);

        DateTimeOffset newExecuteAt = ExecuteAt.AddDays(2);

        await _sut.RescheduleTransitionAsync(EntityType, EntityId, nameof(TestState.Published), newExecuteAt, Ct);

        await _scheduler.Received(1).CancelAsync(
            Arg.Is<ScheduledActionId>(id => id.Value == pendingId), Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ScheduleAsync(
            Arg.Is<ScheduledWorkflowTransitionPayload>(p => p.TargetState == nameof(TestState.Published)),
            newExecuteAt,
            correlation,
            Arg.Any<CancellationToken>());
    }
}
