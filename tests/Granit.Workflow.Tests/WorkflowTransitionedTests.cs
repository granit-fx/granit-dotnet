using Granit.Events;
using Granit.Workflow.Domain;
using Granit.Workflow.Events;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowTransitionedEvent{TState}"/> domain event record.
/// </summary>
public sealed class WorkflowTransitionedTests
{

    [Fact]
    public void Record_ShouldImplementIDomainEvent()
    {
        // Arrange & Act
        WorkflowTransitionedEvent<WorkflowLifecycleStatus> evt = new(
            "Document", "id-1", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.PendingReview, "user-1");

        // Assert
        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        WorkflowTransitionedEvent<WorkflowLifecycleStatus> evt1 = new(
            "Document", "id-1", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published, "user-1");

        WorkflowTransitionedEvent<WorkflowLifecycleStatus> evt2 = new(
            "Document", "id-1", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published, "user-1");

        // Assert
        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void Record_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        WorkflowTransitionedEvent<WorkflowLifecycleStatus> evt1 = new(
            "Document", "id-1", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published, "user-1");

        WorkflowTransitionedEvent<WorkflowLifecycleStatus> evt2 = new(
            "Document", "id-2", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published, "user-1");

        // Assert
        evt1.ShouldNotBe(evt2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        WorkflowTransitionedEvent<WorkflowLifecycleStatus> original = new(
            "Document", "id-1", WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published, "user-1");

        // Act
        WorkflowTransitionedEvent<WorkflowLifecycleStatus> copy = original with { TransitionedBy = "user-2" };

        // Assert
        copy.TransitionedBy.ShouldBe("user-2");
        copy.EntityId.ShouldBe("id-1");
    }
}
