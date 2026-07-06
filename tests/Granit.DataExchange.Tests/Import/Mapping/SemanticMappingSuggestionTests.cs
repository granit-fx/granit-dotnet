using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class SemanticMappingSuggestionTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        SemanticMappingSuggestion a = new("Col", "Prop", 0.85);
        SemanticMappingSuggestion b = new("Col", "Prop", 0.85);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentScores_AreNotEqual()
    {
        SemanticMappingSuggestion a = new("Col", "Prop", 0.85);
        SemanticMappingSuggestion b = new("Col", "Prop", 0.90);

        a.ShouldNotBe(b);
    }
}
