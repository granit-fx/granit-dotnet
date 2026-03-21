using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class TranslationSuggestionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        TranslationSuggestion suggestion = new("fr", "Bonjour");

        suggestion.Culture.ShouldBe("fr");
        suggestion.Value.ShouldBe("Bonjour");
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        TranslationSuggestion suggestion1 = new("fr", "Bonjour");
        TranslationSuggestion suggestion2 = new("fr", "Bonjour");

        suggestion1.ShouldBe(suggestion2);
    }

    [Fact]
    public void Equality_WithDifferentCulture_AreNotEqual()
    {
        TranslationSuggestion suggestion1 = new("fr", "Bonjour");
        TranslationSuggestion suggestion2 = new("en", "Hello");

        suggestion1.ShouldNotBe(suggestion2);
    }

    [Fact]
    public void Equality_WithDifferentValue_AreNotEqual()
    {
        TranslationSuggestion suggestion1 = new("fr", "Bonjour");
        TranslationSuggestion suggestion2 = new("fr", "Salut");

        suggestion1.ShouldNotBe(suggestion2);
    }

    [Fact]
    public void ToString_ContainsCultureAndValue()
    {
        TranslationSuggestion suggestion = new("fr", "Bonjour");

        string result = suggestion.ToString();

        result.ShouldContain("fr");
        result.ShouldContain("Bonjour");
    }
}
