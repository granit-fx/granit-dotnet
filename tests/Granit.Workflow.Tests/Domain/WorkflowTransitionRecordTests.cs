using Granit.Domain;
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

}
