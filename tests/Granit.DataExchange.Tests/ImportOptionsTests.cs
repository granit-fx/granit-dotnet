using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests;

public sealed class ImportOptionsTests
{
    [Fact]
    public void SectionName_is_DataExchange_Import() =>
        ImportOptions.SectionName.ShouldBe("DataExchange:Import");

    [Fact]
    public void Default_max_file_size_is_50()
    {
        ImportOptions options = new();
        options.DefaultMaxFileSizeMb.ShouldBe(50);
    }

    [Fact]
    public void Default_batch_size_is_500()
    {
        ImportOptions options = new();
        options.DefaultBatchSize.ShouldBe(500);
    }

    [Fact]
    public void Default_fuzzy_match_threshold_is_0_8()
    {
        ImportOptions options = new();
        options.FuzzyMatchThreshold.ShouldBe(0.8);
    }

    [Fact]
    public void Properties_are_settable()
    {
        // Arrange & Act
        ImportOptions options = new()
        {
            DefaultMaxFileSizeMb = 100,
            DefaultBatchSize = 1000,
            FuzzyMatchThreshold = 0.7,
        };

        // Assert
        options.DefaultMaxFileSizeMb.ShouldBe(100);
        options.DefaultBatchSize.ShouldBe(1000);
        options.FuzzyMatchThreshold.ShouldBe(0.7);
    }
}
