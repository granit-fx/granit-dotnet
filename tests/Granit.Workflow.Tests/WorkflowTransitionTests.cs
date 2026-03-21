using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowTransition{TState}"/> record.
/// </summary>
public sealed class WorkflowTransitionTests
{
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Act
        WorkflowTransition<WorkflowLifecycleStatus> transition = new()
        {
            From = WorkflowLifecycleStatus.Draft,
            To = WorkflowLifecycleStatus.Published,
            Name = "Publier",
            RequiredPermission = "workflow.publish",
            RequiresApproval = true,
        };

        // Assert
        transition.From.ShouldBe(WorkflowLifecycleStatus.Draft);
        transition.To.ShouldBe(WorkflowLifecycleStatus.Published);
        transition.Name.ShouldBe("Publier");
        transition.RequiredPermission.ShouldBe("workflow.publish");
        transition.RequiresApproval.ShouldBeTrue();
    }

    [Fact]
    public void OptionalProperties_ShouldDefaultToNull()
    {
        // Act
        WorkflowTransition<WorkflowLifecycleStatus> transition = new()
        {
            From = WorkflowLifecycleStatus.Draft,
            To = WorkflowLifecycleStatus.Published,
        };

        // Assert
        transition.Name.ShouldBeNull();
        transition.RequiredPermission.ShouldBeNull();
        transition.RequiresApproval.ShouldBeFalse();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        WorkflowTransition<WorkflowLifecycleStatus> t1 = new()
        {
            From = WorkflowLifecycleStatus.Draft,
            To = WorkflowLifecycleStatus.Published,
            Name = "Publier",
        };

        WorkflowTransition<WorkflowLifecycleStatus> t2 = new()
        {
            From = WorkflowLifecycleStatus.Draft,
            To = WorkflowLifecycleStatus.Published,
            Name = "Publier",
        };

        // Assert
        t1.ShouldBe(t2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        WorkflowTransition<WorkflowLifecycleStatus> original = new()
        {
            From = WorkflowLifecycleStatus.Draft,
            To = WorkflowLifecycleStatus.Published,
            Name = "Publier",
        };

        // Act
        WorkflowTransition<WorkflowLifecycleStatus> copy = original with { RequiresApproval = true };

        // Assert
        copy.RequiresApproval.ShouldBeTrue();
        copy.Name.ShouldBe("Publier");
    }
}
