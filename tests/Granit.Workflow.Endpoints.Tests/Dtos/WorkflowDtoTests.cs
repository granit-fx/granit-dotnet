using Granit.Workflow.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests.Dtos;

/// <summary>
/// Tests for workflow endpoint DTO records.
/// </summary>
public sealed class WorkflowDtoTests
{
    // ========================================================================
    // WorkflowTransitionRequest
    // ========================================================================

    [Fact]
    public void WorkflowTransitionRequest_ShouldSetProperties()
    {
        // Act
        WorkflowTransitionRequest request = new("Published", "Approved by manager.");

        // Assert
        request.TargetState.ShouldBe("Published");
        request.Comment.ShouldBe("Approved by manager.");
    }

    [Fact]
    public void WorkflowTransitionRequest_Comment_CanBeNull()
    {
        // Act
        WorkflowTransitionRequest request = new("Draft", null);

        // Assert
        request.Comment.ShouldBeNull();
    }

    // ========================================================================
    // WorkflowTransitionResultResponse
    // ========================================================================

    [Fact]
    public void WorkflowTransitionResultResponse_ShouldSetProperties()
    {
        // Act
        WorkflowTransitionResultResponse response = new(
            Succeeded: true,
            ResultingState: "Published",
            Outcome: "Completed");

        // Assert
        response.Succeeded.ShouldBeTrue();
        response.ResultingState.ShouldBe("Published");
        response.Outcome.ShouldBe("Completed");
    }

    // ========================================================================
    // WorkflowStatusResponse
    // ========================================================================

    [Fact]
    public void WorkflowStatusResponse_ShouldSetProperties()
    {
        // Arrange
        List<WorkflowTransitionResponse> transitions =
        [
            new("Published", "Publier", true, false),
        ];

        // Act
        WorkflowStatusResponse response = new("Draft", transitions);

        // Assert
        response.CurrentState.ShouldBe("Draft");
        response.AvailableTransitions.Count.ShouldBe(1);
    }

    // ========================================================================
    // WorkflowTransitionResponse
    // ========================================================================

    [Fact]
    public void WorkflowTransitionResponse_ShouldSetAllProperties()
    {
        // Act
        WorkflowTransitionResponse response = new(
            TargetState: "Published",
            Name: "Publier",
            Allowed: true,
            RequiresApproval: false);

        // Assert
        response.TargetState.ShouldBe("Published");
        response.Name.ShouldBe("Publier");
        response.Allowed.ShouldBeTrue();
        response.RequiresApproval.ShouldBeFalse();
    }

    [Fact]
    public void WorkflowTransitionResponse_WithApproval_ShouldSetCorrectly()
    {
        // Act
        WorkflowTransitionResponse response = new(
            TargetState: "Published",
            Name: "Demander l'approbation",
            Allowed: false,
            RequiresApproval: true);

        // Assert
        response.Allowed.ShouldBeFalse();
        response.RequiresApproval.ShouldBeTrue();
    }
}
