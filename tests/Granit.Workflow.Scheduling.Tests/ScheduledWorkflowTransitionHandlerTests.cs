using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Scheduling.Tests;

public sealed class ScheduledWorkflowTransitionHandlerTests
{
    [Fact]
    public async Task Handle_resolves_applier_by_entity_type_and_applies_transition()
    {
        var entityId = Guid.NewGuid();
        var payload = new ScheduledWorkflowTransitionPayload("BlogPost", entityId, nameof(TestState.Published));

        IWorkflowTransitionApplier applier = Substitute.For<IWorkflowTransitionApplier>();
        IWorkflowTransitionApplierRegistry registry = Substitute.For<IWorkflowTransitionApplierRegistry>();
        registry.Resolve("BlogPost").Returns(applier);

        await ScheduledWorkflowTransitionHandler.Handle(
            payload,
            registry,
            NullLogger<ScheduledWorkflowTransitionHandler>.Instance,
            CancellationToken.None);

        registry.Received(1).Resolve("BlogPost");
        await applier.Received(1).ApplyAsync(entityId, nameof(TestState.Published), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_propagates_registry_resolution_failure()
    {
        var payload = new ScheduledWorkflowTransitionPayload("Unknown", Guid.NewGuid(), nameof(TestState.Published));
        IWorkflowTransitionApplierRegistry registry = Substitute.For<IWorkflowTransitionApplierRegistry>();
        registry.Resolve("Unknown").Returns(_ => throw new InvalidOperationException("no applier"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ScheduledWorkflowTransitionHandler.Handle(
                payload,
                registry,
                NullLogger<ScheduledWorkflowTransitionHandler>.Instance,
                CancellationToken.None));
    }
}
