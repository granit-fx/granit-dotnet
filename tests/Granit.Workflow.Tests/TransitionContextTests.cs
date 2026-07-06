using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="TransitionContext"/> record.
/// </summary>
public sealed class TransitionContextTests
{
    [Fact]
    public void Comment_DefaultsToNull()
    {
        // Arrange & Act
        TransitionContext context = new();

        // Assert
        context.Comment.ShouldBeNull();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        TransitionContext ctx1 = new() { Comment = "test" };
        TransitionContext ctx2 = new() { Comment = "test" };

        // Assert
        ctx1.ShouldBe(ctx2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        TransitionContext original = new() { Comment = "original" };

        // Act
        TransitionContext copy = original with { Comment = "updated" };

        // Assert
        copy.Comment.ShouldBe("updated");
        original.Comment.ShouldBe("original");
    }
}
