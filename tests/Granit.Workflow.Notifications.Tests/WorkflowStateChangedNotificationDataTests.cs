using Shouldly;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

/// <summary>
/// Tests for <see cref="WorkflowStateChangedNotificationData"/> record.
/// </summary>
public sealed class WorkflowStateChangedNotificationDataTests
{
    [Fact]
    public void Properties_ShouldMatchConstructorParameters()
    {
        // Act
        WorkflowStateChangedNotificationData data = new(
            EntityType: "Publication",
            EntityId: "42",
            PreviousState: "Draft",
            NewState: "Published",
            TransitionedBy: "user-1");

        // Assert
        data.EntityType.ShouldBe("Publication");
        data.EntityId.ShouldBe("42");
        data.PreviousState.ShouldBe("Draft");
        data.NewState.ShouldBe("Published");
        data.TransitionedBy.ShouldBe("user-1");
    }

    [Fact]
    public void Equality_TwoIdenticalRecords_AreEqual()
    {
        // Arrange
        WorkflowStateChangedNotificationData a = new("Pub", "1", "Draft", "Published", "user");
        WorkflowStateChangedNotificationData b = new("Pub", "1", "Draft", "Published", "user");

        // Assert
        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        WorkflowStateChangedNotificationData a = new("Pub", "1", "Draft", "Published", "user-1");
        WorkflowStateChangedNotificationData b = new("Pub", "1", "Draft", "Published", "user-2");

        // Assert
        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        WorkflowStateChangedNotificationData original = new("Pub", "1", "Draft", "Published", "user");

        // Act
        WorkflowStateChangedNotificationData copy = original with { NewState = "Archived" };

        // Assert
        copy.NewState.ShouldBe("Archived");
        copy.EntityType.ShouldBe("Pub");
    }
}
