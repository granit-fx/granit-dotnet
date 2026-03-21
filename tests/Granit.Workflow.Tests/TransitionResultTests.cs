using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="TransitionResult{TState}"/> and <see cref="TransitionOutcome"/>.
/// </summary>
public sealed class TransitionResultTests
{
    [Fact]
    public void TransitionResult_ShouldSetAllProperties()
    {
        // Act
        TransitionResult<WorkflowLifecycleStatus> result = new()
        {
            Succeeded = true,
            ResultingState = WorkflowLifecycleStatus.Published,
            Outcome = TransitionOutcome.Completed,
        };

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.Published);
        result.Outcome.ShouldBe(TransitionOutcome.Completed);
    }

    [Theory]
    [InlineData(TransitionOutcome.Completed)]
    [InlineData(TransitionOutcome.ApprovalRequested)]
    [InlineData(TransitionOutcome.Denied)]
    [InlineData(TransitionOutcome.InvalidTransition)]
    public void TransitionOutcome_AllValues_ShouldBeValid(TransitionOutcome outcome) =>
        // Assert
        Enum.IsDefined(outcome).ShouldBeTrue();

    [Fact]
    public void TransitionOutcome_ShouldHaveExactlyFourValues()
    {
        // Assert
        string[] names = Enum.GetNames<TransitionOutcome>();
        names.Length.ShouldBe(4);
    }

    [Fact]
    public void TransitionResult_ShouldSupportValueEquality()
    {
        // Arrange
        TransitionResult<WorkflowLifecycleStatus> result1 = new()
        {
            Succeeded = true,
            ResultingState = WorkflowLifecycleStatus.Published,
            Outcome = TransitionOutcome.Completed,
        };

        TransitionResult<WorkflowLifecycleStatus> result2 = new()
        {
            Succeeded = true,
            ResultingState = WorkflowLifecycleStatus.Published,
            Outcome = TransitionOutcome.Completed,
        };

        // Assert
        result1.ShouldBe(result2);
    }

    [Fact]
    public void TransitionResult_DifferentOutcome_ShouldNotBeEqual()
    {
        // Arrange
        TransitionResult<WorkflowLifecycleStatus> result1 = new()
        {
            Succeeded = true,
            ResultingState = WorkflowLifecycleStatus.Published,
            Outcome = TransitionOutcome.Completed,
        };

        TransitionResult<WorkflowLifecycleStatus> result2 = new()
        {
            Succeeded = true,
            ResultingState = WorkflowLifecycleStatus.Published,
            Outcome = TransitionOutcome.ApprovalRequested,
        };

        // Assert
        result1.ShouldNotBe(result2);
    }
}
