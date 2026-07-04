using Shouldly;
using Xunit;

namespace Granit.Workflow.Scheduling.Tests;

public sealed class WorkflowTransitionApplierBaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeEntity(Guid id, TestState state)
    {
        public Guid Id { get; } = id;
        public TestState State { get; private set; } = state;
        public void SetState(TestState state) => State = state;
    }

    private sealed class FakeApplier(FakeEntity? entity)
        : WorkflowTransitionApplierBase<FakeEntity, TestState>(TestWorkflow.Definition)
    {
        public FakeEntity? Saved { get; private set; }

        protected override Task<FakeEntity?> LoadAsync(Guid entityId, CancellationToken cancellationToken) =>
            Task.FromResult(entity);

        protected override TestState GetCurrentState(FakeEntity e) => e.State;

        protected override void SetState(FakeEntity e, TestState target) => e.SetState(target);

        protected override Task SaveAsync(FakeEntity e, CancellationToken cancellationToken)
        {
            Saved = e;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ApplyAsync_applies_and_saves_a_valid_transition()
    {
        var entity = new FakeEntity(Guid.NewGuid(), TestState.Draft);
        var applier = new FakeApplier(entity);

        await applier.ApplyAsync(entity.Id, nameof(TestState.Published), Ct);

        entity.State.ShouldBe(TestState.Published);
        applier.Saved.ShouldBeSameAs(entity);
    }

    [Fact]
    public async Task ApplyAsync_is_idempotent_when_already_in_target_state()
    {
        var entity = new FakeEntity(Guid.NewGuid(), TestState.Published);
        var applier = new FakeApplier(entity);

        await applier.ApplyAsync(entity.Id, nameof(TestState.Published), Ct);

        applier.Saved.ShouldBeNull(); // no save for a no-op
    }

    [Fact]
    public async Task ApplyAsync_throws_for_transition_not_defined_from_current_state()
    {
        // Draft -> Archived is not a defined transition (only Draft -> Published).
        var entity = new FakeEntity(Guid.NewGuid(), TestState.Draft);
        var applier = new FakeApplier(entity);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            applier.ApplyAsync(entity.Id, nameof(TestState.Archived), Ct));
        applier.Saved.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_throws_for_unparseable_target_state()
    {
        var entity = new FakeEntity(Guid.NewGuid(), TestState.Draft);
        var applier = new FakeApplier(entity);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            applier.ApplyAsync(entity.Id, "NotAState", Ct));
    }

    [Fact]
    public async Task ApplyAsync_throws_by_default_when_entity_not_found()
    {
        var applier = new FakeApplier(entity: null);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            applier.ApplyAsync(Guid.NewGuid(), nameof(TestState.Published), Ct));
    }
}
