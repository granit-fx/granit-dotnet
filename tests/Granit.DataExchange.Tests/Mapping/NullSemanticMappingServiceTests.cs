using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class NullSemanticMappingServiceTests
{
    [Fact]
    public void IsAvailable_returns_false()
    {
        // Arrange
        NullSemanticMappingService sut = new();

        // Assert
        sut.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_returns_empty_list()
    {
        // Arrange
        NullSemanticMappingService sut = new();

        // Act
        IReadOnlyList<SemanticMappingSuggestion> result = await sut.SuggestSemanticMappingsAsync(
            ["Col1", "Col2"],
            [new ImportFieldMetadata("Name", "String", null, null, false)],
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }
}
