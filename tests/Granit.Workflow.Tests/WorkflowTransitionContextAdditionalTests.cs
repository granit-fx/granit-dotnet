using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Additional tests for <see cref="WorkflowTransitionContext"/> edge cases.
/// </summary>
public sealed class WorkflowTransitionContextAdditionalTests
{
    [Fact]
    public void SetComment_WithNull_ShouldSetNullComment()
    {
        // Act
        using IDisposable scope = WorkflowTransitionContext.SetComment(null);

        // Assert
        WorkflowTransitionContext.Current.ShouldNotBeNull();
        WorkflowTransitionContext.Current!.Comment.ShouldBeNull();
    }

    [Fact]
    public void NestedScopes_ShouldRestoreCorrectly()
    {
        // Arrange
        using IDisposable outer = WorkflowTransitionContext.SetComment("outer");

        // Act
        IDisposable inner = WorkflowTransitionContext.SetComment("inner");
        WorkflowTransitionContext.Current!.Comment.ShouldBe("inner");

        inner.Dispose();

        // Assert — should restore to outer
        WorkflowTransitionContext.Current.ShouldNotBeNull();
        WorkflowTransitionContext.Current!.Comment.ShouldBe("outer");
    }

    [Fact]
    public void DoubleDispose_ShouldBeIdempotent()
    {
        // Arrange
        IDisposable scope = WorkflowTransitionContext.SetComment("test");

        // Act — dispose twice
        scope.Dispose();
        scope.Dispose();

        // Assert — should not throw and Current should be null
        WorkflowTransitionContext.Current.ShouldBeNull();
    }

    [Fact]
    public void TransitionInfo_ShouldSupportValueEquality()
    {
        // Arrange
        using IDisposable scope1 = WorkflowTransitionContext.SetComment("comment");
        WorkflowTransitionContext.TransitionInfo? info1 = WorkflowTransitionContext.Current;

        // Create second identical info
        WorkflowTransitionContext.TransitionInfo info2 = new() { Comment = "comment" };

        // Assert
        info1.ShouldBe(info2);
    }

    [Fact]
    public void Current_WithoutScope_ShouldBeNull() =>
        // Ensure clean state
        WorkflowTransitionContext.Current.ShouldBeNull();
}
