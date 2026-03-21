using Granit.Core.Domain;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests.Domain;

/// <summary>
/// Tests for <see cref="WorkflowTransitionRecord"/> entity.
/// </summary>
public sealed class WorkflowTransitionRecordTests
{
    [Fact]
    public void ShouldInheritFromEntity()
    {
        // Arrange & Act
        WorkflowTransitionRecord record = new();

        // Assert
        record.ShouldBeAssignableTo<Entity>();
    }

    [Fact]
    public void DefaultValues_ShouldBeEmptyStrings()
    {
        // Arrange & Act
        WorkflowTransitionRecord record = new();

        // Assert
        record.EntityType.ShouldBe(string.Empty);
        record.EntityId.ShouldBe(string.Empty);
        record.PreviousState.ShouldBe(string.Empty);
        record.NewState.ShouldBe(string.Empty);
        record.TransitionedBy.ShouldBe(string.Empty);
        record.Comment.ShouldBeNull();
        record.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Properties_ShouldBeMutable()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Act
        WorkflowTransitionRecord record = new()
        {
            Id = id,
            EntityType = "Invoice",
            EntityId = "inv-123",
            PreviousState = "Draft",
            NewState = "Published",
            TransitionedAt = now,
            TransitionedBy = "user-42",
            Comment = "Approved",
            TenantId = tenantId,
        };

        // Assert
        record.Id.ShouldBe(id);
        record.EntityType.ShouldBe("Invoice");
        record.EntityId.ShouldBe("inv-123");
        record.PreviousState.ShouldBe("Draft");
        record.NewState.ShouldBe("Published");
        record.TransitionedAt.ShouldBe(now);
        record.TransitionedBy.ShouldBe("user-42");
        record.Comment.ShouldBe("Approved");
        record.TenantId.ShouldBe(tenantId);
    }
}
