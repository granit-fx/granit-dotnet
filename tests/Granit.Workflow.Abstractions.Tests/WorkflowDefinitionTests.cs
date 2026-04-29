// =============================================================================
// Tests - WorkflowDefinition (in Abstractions)
// =============================================================================
// Smoke tests covering the fluent builder + transition declaration. Detailed
// engine-level tests live in Granit.Workflow.Tests.
// =============================================================================

using Granit.Workflow;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Abstractions.Tests;

public sealed class WorkflowDefinitionTests
{
    [Fact]
    public void Create_BuildsImmutableDefinition_WithInitialStateAndTransitions()
    {
        var definition = WorkflowDefinition<DraftState>.Create(b => b
            .InitialState(DraftState.Draft)
            .Transition(DraftState.Draft, DraftState.Submitted)
            .Transition(DraftState.Submitted, DraftState.Approved)
            .Transition(DraftState.Submitted, DraftState.Rejected));

        definition.InitialState.ShouldBe(DraftState.Draft);
        definition.Transitions.Count.ShouldBe(3);
    }

    [Fact]
    public void GetAllowedTransitions_ReturnsTransitionsFromMatchingSource()
    {
        var definition = WorkflowDefinition<DraftState>.Create(b => b
            .InitialState(DraftState.Draft)
            .Transition(DraftState.Draft, DraftState.Submitted)
            .Transition(DraftState.Submitted, DraftState.Approved)
            .Transition(DraftState.Submitted, DraftState.Rejected));

        IReadOnlyList<WorkflowTransition<DraftState>> fromSubmitted =
            definition.GetAllowedTransitions(DraftState.Submitted);

        fromSubmitted.Count.ShouldBe(2);
        fromSubmitted.ShouldContain(t => t.To == DraftState.Approved);
        fromSubmitted.ShouldContain(t => t.To == DraftState.Rejected);
    }

    [Fact]
    public void Create_WithoutInitialState_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            WorkflowDefinition<DraftState>.Create(b => b
                .Transition(DraftState.Draft, DraftState.Submitted)));
    }

    [Fact]
    public void Create_WithoutTransitions_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            WorkflowDefinition<DraftState>.Create(b => b.InitialState(DraftState.Draft)));
    }

    [Fact]
    public void Create_WithDuplicateTransition_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            WorkflowDefinition<DraftState>.Create(b => b
                .InitialState(DraftState.Draft)
                .Transition(DraftState.Draft, DraftState.Submitted)
                .Transition(DraftState.Draft, DraftState.Submitted)));
    }

    [Fact]
    public void Create_WithUnreachableState_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            WorkflowDefinition<DraftState>.Create(b => b
                .InitialState(DraftState.Draft)
                .Transition(DraftState.Submitted, DraftState.Approved)));
    }

    [Fact]
    public void Transition_WithRequiredPermissionAndApproval_PropagatesToTransitionRecord()
    {
        var definition = WorkflowDefinition<DraftState>.Create(b => b
            .InitialState(DraftState.Draft)
            .Transition(DraftState.Draft, DraftState.Submitted, t => t
                .Named("Submit for review")
                .RequiresPermission("documents.submit")
                .RequiresApproval()));

        WorkflowTransition<DraftState> transition = definition.Transitions[0];
        transition.Name.ShouldBe("Submit for review");
        transition.RequiredPermission.ShouldBe("documents.submit");
        transition.RequiresApproval.ShouldBeTrue();
    }

    [Fact]
    public void GranitWorkflowAbstractionsModule_IsAGranitModule()
    {
        // Sanity check — base modules pull this module via [DependsOn(...)] to
        // declare workflow shapes without taking a runtime dep on Granit.Workflow.
        typeof(Granit.Modularity.GranitModule)
            .IsAssignableFrom(typeof(GranitWorkflowAbstractionsModule))
            .ShouldBeTrue();
    }

    private enum DraftState
    {
        Draft,
        Submitted,
        Approved,
        Rejected,
    }
}
