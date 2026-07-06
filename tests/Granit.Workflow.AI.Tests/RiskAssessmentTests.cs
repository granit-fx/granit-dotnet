using Shouldly;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Tests for <see cref="RiskAssessment"/> record.
/// </summary>
public sealed class RiskAssessmentTests
{

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        RiskAssessment a1 = new(0.5, "Medium risk.", ["Factor A"]);
        RiskAssessment a2 = new(0.5, "Medium risk.", ["Factor A"]);

        // Assert — record equality uses reference equality for collections,
        // so same list reference is needed for equality
        a1.ShouldNotBe(a2); // Different list instances
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        RiskAssessment original = new(0.5, "Medium risk.", ["Factor A"]);

        // Act
        RiskAssessment copy = original with { RiskScore = 0.9 };

        // Assert
        copy.RiskScore.ShouldBe(0.9);
        copy.Reasoning.ShouldBe("Medium risk.");
    }
}
