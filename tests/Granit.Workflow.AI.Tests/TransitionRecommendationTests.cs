using Shouldly;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Tests for <see cref="TransitionRecommendation"/> record.
/// </summary>
public sealed class TransitionRecommendationTests
{
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Act
        TransitionRecommendation recommendation = new(
            RecommendedTransition: "Publish",
            Reasoning: "Document is complete.",
            Confidence: 0.95);

        // Assert
        recommendation.RecommendedTransition.ShouldBe("Publish");
        recommendation.Reasoning.ShouldBe("Document is complete.");
        recommendation.Confidence.ShouldBe(0.95);
    }

    [Fact]
    public void Record_ShouldSupportValueEquality()
    {
        // Arrange
        TransitionRecommendation r1 = new("Publish", "Complete", 0.9);
        TransitionRecommendation r2 = new("Publish", "Complete", 0.9);

        // Assert
        r1.ShouldBe(r2);
    }

    [Fact]
    public void Record_ShouldSupportWith()
    {
        // Arrange
        TransitionRecommendation original = new("Publish", "Complete", 0.9);

        // Act
        TransitionRecommendation copy = original with { Confidence = 0.5 };

        // Assert
        copy.Confidence.ShouldBe(0.5);
        copy.RecommendedTransition.ShouldBe("Publish");
    }
}
