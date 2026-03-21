using Granit.Workflow.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="TransitionHistoryResponse"/> record.
/// </summary>
public sealed class TransitionHistoryResponseTests
{
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Act
        TransitionHistoryResponse response = new(
            PreviousState: "Draft",
            NewState: "Published",
            TransitionedAt: now,
            TransitionedBy: "user-42",
            Comment: "Approved by manager");

        // Assert
        response.PreviousState.ShouldBe("Draft");
        response.NewState.ShouldBe("Published");
        response.TransitionedAt.ShouldBe(now);
        response.TransitionedBy.ShouldBe("user-42");
        response.Comment.ShouldBe("Approved by manager");
    }

    [Fact]
    public void Comment_CanBeNull()
    {
        // Act
        TransitionHistoryResponse response = new(
            "Draft", "Published", DateTimeOffset.UtcNow, "user", null);

        // Assert
        response.Comment.ShouldBeNull();
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TransitionHistoryResponse r1 = new("A", "B", now, "user", null);
        TransitionHistoryResponse r2 = new("A", "B", now, "user", null);

        // Assert
        r1.ShouldBe(r2);
    }
}
