using Granit.Workflow.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests.Domain;

/// <summary>
/// Tests for <see cref="WorkflowStateName"/> value object.
/// </summary>
public sealed class WorkflowStateNameTests
{
    // ========================================================================
    // Create — valid inputs
    // ========================================================================

    [Fact]
    public void Create_WithValidValue_ShouldReturnInstance()
    {
        // Act
        var name = WorkflowStateName.Create("Draft");

        // Assert
        name.Value.ShouldBe("Draft");
    }

    [Fact]
    public void Create_AtMaxLength_ShouldSucceed()
    {
        // Arrange
        string value = new('A', 100);

        // Act
        var name = WorkflowStateName.Create(value);

        // Assert
        name.Value.ShouldBe(value);
    }

    // ========================================================================
    // Create — invalid inputs
    // ========================================================================

    [Fact]
    public void Create_WithNull_ShouldThrowArgumentException() =>
        // Act & Assert
        Should.Throw<ArgumentException>(() => WorkflowStateName.Create(null!));

    [Fact]
    public void Create_WithEmptyString_ShouldThrowArgumentException() =>
        // Act & Assert
        Should.Throw<ArgumentException>(() => WorkflowStateName.Create(string.Empty));

    [Fact]
    public void Create_WithWhitespace_ShouldThrowArgumentException() =>
        // Act & Assert
        Should.Throw<ArgumentException>(() => WorkflowStateName.Create("   "));

    [Fact]
    public void Create_ExceedingMaxLength_ShouldThrowArgumentException()
    {
        // Arrange
        string value = new('A', 101);

        // Act & Assert
        ArgumentException exception = Should.Throw<ArgumentException>(() => WorkflowStateName.Create(value));
        exception.Message.ShouldContain("100");
    }

    // ========================================================================
    // Implicit operators
    // ========================================================================

    [Fact]
    public void ImplicitConversion_ToString_ShouldReturnValue()
    {
        // Arrange
        var name = WorkflowStateName.Create("Published");

        // Act
        string result = name;

        // Assert
        result.ShouldBe("Published");
    }

    [Fact]
    public void ImplicitConversion_FromString_ShouldCreateInstance()
    {
        // Act
        WorkflowStateName name = "Archived";

        // Assert
        name.Value.ShouldBe("Archived");
    }

    // ========================================================================
    // Equality
    // ========================================================================

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        // Arrange
        var name1 = WorkflowStateName.Create("Draft");
        var name2 = WorkflowStateName.Create("Draft");

        // Assert
        name1.ShouldBe(name2);
    }

    [Fact]
    public void Equality_DifferentValue_ShouldNotBeEqual()
    {
        // Arrange
        var name1 = WorkflowStateName.Create("Draft");
        var name2 = WorkflowStateName.Create("Published");

        // Assert
        name1.ShouldNotBe(name2);
    }
}
