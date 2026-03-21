using Shouldly;
using Xunit;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Tests for <see cref="RiskAssessment"/> record.
/// </summary>
public sealed class RiskAssessmentTests
{
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        List<string> factors = ["Missing approval", "High amount"];

        // Act
        RiskAssessment assessment = new(
            RiskScore: 0.85,
            Reasoning: "Financial risk detected.",
            RiskFactors: factors);

        // Assert
        assessment.RiskScore.ShouldBe(0.85);
        assessment.Reasoning.ShouldBe("Financial risk detected.");
        assessment.RiskFactors.Count.ShouldBe(2);
        assessment.RiskFactors[0].ShouldBe("Missing approval");
    }

    [Fact]
    public void EmptyRiskFactors_ShouldBeValid()
    {
        // Act
        RiskAssessment assessment = new(0.0, "No risk.", []);

        // Assert
        assessment.RiskFactors.ShouldBeEmpty();
    }

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
