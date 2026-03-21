using System.Text.Json.Nodes;
using Granit.Workflow.Endpoints.Dtos;
using Granit.Workflow.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Tests for <see cref="WorkflowSchemaExampleProvider"/>.
/// </summary>
public sealed class WorkflowSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ShouldContainWorkflowTransitionRequestExample()
    {
        // Arrange
        WorkflowSchemaExampleProvider provider = new();

        // Act
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        // Assert
        examples.ShouldContainKey(typeof(WorkflowTransitionRequest));
    }

    [Fact]
    public void GetExamples_WorkflowTransitionRequest_ShouldHaveTargetState()
    {
        // Arrange
        WorkflowSchemaExampleProvider provider = new();

        // Act
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(WorkflowTransitionRequest)];

        // Assert
        string? targetState = example["targetState"]?.GetValue<string>();
        targetState.ShouldBe("Published");
    }

    [Fact]
    public void GetExamples_WorkflowTransitionRequest_ShouldHaveComment()
    {
        // Arrange
        WorkflowSchemaExampleProvider provider = new();

        // Act
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(WorkflowTransitionRequest)];

        // Assert
        string? comment = example["comment"]?.GetValue<string>();
        comment.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetExamples_ShouldReturnExactlyOneExample()
    {
        // Arrange
        WorkflowSchemaExampleProvider provider = new();

        // Act
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        // Assert
        examples.Count.ShouldBe(1);
    }
}
