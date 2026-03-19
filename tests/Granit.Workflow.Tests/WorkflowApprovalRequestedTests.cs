using Granit.Core.Events;
using Granit.Workflow.Events;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowApprovalRequestedEvent"/> domain event record.
/// </summary>
public sealed class WorkflowApprovalRequestedTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange & Act
        WorkflowApprovalRequestedEvent evt = new(
            EntityType: "Invoice",
            EntityId: "inv-456",
            RequestedBy: "user-7",
            TargetState: "Published",
            RequiredPermission: "workflow.publish");

        // Assert
        evt.EntityType.ShouldBe("Invoice");
        evt.EntityId.ShouldBe("inv-456");
        evt.RequestedBy.ShouldBe("user-7");
        evt.TargetState.ShouldBe("Published");
        evt.RequiredPermission.ShouldBe("workflow.publish");
    }

    [Fact]
    public void Record_ShouldImplementIDomainEvent()
    {
        // Arrange & Act
        WorkflowApprovalRequestedEvent evt = new(
            "Document", "id-1", "user-1", "Published", "workflow.publish");

        // Assert
        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        WorkflowApprovalRequestedEvent evt1 = new(
            "Document", "id-1", "user-1", "Published", "workflow.publish");

        WorkflowApprovalRequestedEvent evt2 = new(
            "Document", "id-1", "user-1", "Published", "workflow.publish");

        // Assert
        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void Record_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        WorkflowApprovalRequestedEvent evt1 = new(
            "Document", "id-1", "user-1", "Published", "workflow.publish");

        WorkflowApprovalRequestedEvent evt2 = new(
            "Document", "id-1", "user-2", "Published", "workflow.publish");

        // Assert
        evt1.ShouldNotBe(evt2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        WorkflowApprovalRequestedEvent original = new(
            "Document", "id-1", "user-1", "Published", "workflow.publish");

        // Act
        WorkflowApprovalRequestedEvent copy = original with { RequestedBy = "user-99" };

        // Assert
        copy.RequestedBy.ShouldBe("user-99");
        copy.EntityType.ShouldBe("Document");
    }
}
