using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class SemanticMappingSuggestionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        SemanticMappingSuggestion suggestion = new("Courriel", "Email", 0.92);

        suggestion.SourceColumn.ShouldBe("Courriel");
        suggestion.TargetProperty.ShouldBe("Email");
        suggestion.Score.ShouldBe(0.92);
    }

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
