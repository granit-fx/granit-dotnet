namespace Granit.Workflow.Scheduling.Tests;

/// <summary>Test workflow states used across the scheduling bridge tests.</summary>
internal enum TestState
{
    Draft,
    Published,
    Archived,
}

/// <summary>A minimal reachable workflow definition: Draft → Published → Archived.</summary>
internal static class TestWorkflow
{
    public static WorkflowDefinition<TestState> Definition { get; } =
        WorkflowDefinition<TestState>.Create(builder => builder
            .InitialState(TestState.Draft)
            .Transition(TestState.Draft, TestState.Published)
            .Transition(TestState.Published, TestState.Archived));
}
