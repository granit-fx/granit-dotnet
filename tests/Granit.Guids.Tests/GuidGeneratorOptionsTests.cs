using Granit.Guids.Options;
using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class GuidGeneratorOptionsTests
{
    [Fact]
    public void Strategy_DefaultsToUuidV7()
    {
        // Arrange
        GuidGeneratorOptions options = new();

        // Assert
        options.Strategy.ShouldBe(GuidStrategy.UuidV7);
    }

    [Fact]
    public void DefaultSequentialGuidType_DefaultsToNull()
    {
        // Arrange
        GuidGeneratorOptions options = new();

        // Assert
        options.DefaultSequentialGuidType.ShouldBeNull();
    }

    [Fact]
    public void GetDefaultSequentialGuidType_WhenNull_ReturnsSequentialAsString()
    {
        // Arrange
        GuidGeneratorOptions options = new() { DefaultSequentialGuidType = null };

        // Act
        SequentialGuidType result = options.GetDefaultSequentialGuidType();

        // Assert
        result.ShouldBe(SequentialGuidType.SequentialAsString);
    }

    [Fact]
    public void GetDefaultSequentialGuidType_WhenSequentialAsBinary_ReturnsSequentialAsBinary()
    {
        // Arrange
        GuidGeneratorOptions options = new() { DefaultSequentialGuidType = SequentialGuidType.SequentialAsBinary };

        // Act
        SequentialGuidType result = options.GetDefaultSequentialGuidType();

        // Assert
        result.ShouldBe(SequentialGuidType.SequentialAsBinary);
    }

    [Fact]
    public void GetDefaultSequentialGuidType_WhenSequentialAtEnd_ReturnsSequentialAtEnd()
    {
        // Arrange
        GuidGeneratorOptions options = new() { DefaultSequentialGuidType = SequentialGuidType.SequentialAtEnd };

        // Act
        SequentialGuidType result = options.GetDefaultSequentialGuidType();

        // Assert
        result.ShouldBe(SequentialGuidType.SequentialAtEnd);
    }

    [Fact]
    public void GetDefaultSequentialGuidType_WhenSequentialAsString_ReturnsSequentialAsString()
    {
        // Arrange
        GuidGeneratorOptions options = new() { DefaultSequentialGuidType = SequentialGuidType.SequentialAsString };

        // Act
        SequentialGuidType result = options.GetDefaultSequentialGuidType();

        // Assert
        result.ShouldBe(SequentialGuidType.SequentialAsString);
    }

    [Theory]
    [InlineData(GuidStrategy.UuidV7)]
    [InlineData(GuidStrategy.Sequential)]
    [InlineData(GuidStrategy.Random)]
    public void Strategy_SetAndGet_RoundTrips(GuidStrategy strategy)
    {
        // Arrange
        GuidGeneratorOptions options = new() { Strategy = strategy };

        // Assert
        options.Strategy.ShouldBe(strategy);
    }
}
