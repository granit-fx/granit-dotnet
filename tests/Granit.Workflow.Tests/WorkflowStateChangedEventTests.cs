using Granit.Core.Events;
using Granit.Workflow.Events;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowStateChangedEvent"/> domain event record.
/// </summary>
public sealed class WorkflowStateChangedEventTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange & Act
        WorkflowStateChangedEvent evt = new(
            EntityType: "Publication",
            EntityId: "pub-42",
            PreviousState: "Draft",
            NewState: "Published",
            TransitionedBy: "user-1");

        // Assert
        evt.EntityType.ShouldBe("Publication");
        evt.EntityId.ShouldBe("pub-42");
        evt.PreviousState.ShouldBe("Draft");
        evt.NewState.ShouldBe("Published");
        evt.TransitionedBy.ShouldBe("user-1");
    }

    [Fact]
    public void Record_ShouldImplementIDomainEvent()
    {
        // Arrange & Act
        WorkflowStateChangedEvent evt = new("Doc", "1", "A", "B", "user");

        // Assert
        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        WorkflowStateChangedEvent evt1 = new("Doc", "1", "A", "B", "user");
        WorkflowStateChangedEvent evt2 = new("Doc", "1", "A", "B", "user");

        // Assert
        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void Record_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        WorkflowStateChangedEvent evt1 = new("Doc", "1", "A", "B", "user-1");
        WorkflowStateChangedEvent evt2 = new("Doc", "1", "A", "B", "user-2");

        // Assert
        evt1.ShouldNotBe(evt2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        WorkflowStateChangedEvent original = new("Doc", "1", "A", "B", "user-1");

        // Act
        WorkflowStateChangedEvent copy = original with { NewState = "C" };

        // Assert
        copy.NewState.ShouldBe("C");
        copy.EntityType.ShouldBe("Doc");
    }
}
