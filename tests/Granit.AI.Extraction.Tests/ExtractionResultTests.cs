using Shouldly;

namespace Granit.AI.Extraction.Tests;

public sealed class ExtractionResultTests
{
    private sealed record SampleData
    {
        public string? Name { get; init; }
        public int Value { get; init; }
    }

    [Fact]
    public void Success_SetsCorrectProperties()
    {
        // Arrange
        var data = new SampleData { Name = "Test", Value = 42 };
        List<string> warnings = ["minor warning"];

        // Act
        var result = ExtractionResult.Success(data, 0.95, warnings);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Succeeded);
        result.Data.ShouldBe(data);
        result.ConfidenceScore.ShouldBe(0.95);
        result.ErrorMessage.ShouldBeNull();
        result.Warnings.ShouldBe(warnings);
    }

    [Fact]
    public void Success_WithoutWarnings_DefaultsToEmptyList()
    {
        // Arrange
        var data = new SampleData { Name = "Test", Value = 1 };

        // Act
        var result = ExtractionResult.Success(data, 0.9);

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Failed_SetsCorrectProperties()
    {
        // Act
        var result = ExtractionResult.Failed<SampleData>("Something went wrong");

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.Data.ShouldBeNull();
        result.ConfidenceScore.ShouldBeNull();
        result.ErrorMessage.ShouldBe("Something went wrong");
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void NeedsReview_SetsCorrectProperties()
    {
        // Arrange
        var data = new SampleData { Name = "Uncertain", Value = 0 };
        List<string> warnings = ["Low confidence on Name field"];

        // Act
        var result = ExtractionResult.NeedsReview(data, 0.55, warnings);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.NeedsReview);
        result.Data.ShouldBe(data);
        result.ConfidenceScore.ShouldBe(0.55);
        result.ErrorMessage.ShouldBeNull();
        result.Warnings.ShouldBe(warnings);
    }
}
