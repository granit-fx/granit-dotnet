using Shouldly;

using Xunit;

namespace Granit.Localization.Tests;

public sealed class JsonLocalizationDictionaryBuilderTests
{
    [Fact]
    public void Build_ParsesEmbeddedJsonFiles()
    {
        // Arrange — les fichiers Test/fr.json et Test/en.json sont embarqués dans l'assembly de test
        System.Reflection.Assembly assembly = typeof(JsonLocalizationDictionaryBuilderTests).Assembly;
        const string prefix = "Granit.Localization.Tests.TestResources.Localization.Test";

        // Act
        Dictionary<string, Dictionary<string, string>> result =
            Internal.JsonLocalizationDictionaryBuilder.Build(assembly, prefix);

        // Assert
        result.ShouldContainKey("fr");
        result.ShouldContainKey("en");
        result["fr"].ShouldContainKey("Test:Hello");
        result["fr"]["Test:Hello"].ShouldBe("Bonjour");
        result["en"]["Test:Hello"].ShouldBe("Hello");
    }

    [Fact]
    public void Build_ReturnsEmptyForNonExistentPrefix()
    {
        // Arrange
        System.Reflection.Assembly assembly = typeof(JsonLocalizationDictionaryBuilderTests).Assembly;

        // Act
        Dictionary<string, Dictionary<string, string>> result =
            Internal.JsonLocalizationDictionaryBuilder.Build(assembly, "NonExistent.Prefix");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_ParsesParameterizedValues()
    {
        // Arrange
        System.Reflection.Assembly assembly = typeof(JsonLocalizationDictionaryBuilderTests).Assembly;
        const string prefix = "Granit.Localization.Tests.TestResources.Localization.Test";

        // Act
        Dictionary<string, Dictionary<string, string>> result =
            Internal.JsonLocalizationDictionaryBuilder.Build(assembly, prefix);

        // Assert
        result["fr"]["Test:Welcome"].ShouldContain("{0}");
        result["fr"]["Test:Welcome"].ShouldContain("{1}");
    }

    [Fact]
    public void Build_ParsesParentResourceFiles()
    {
        // Arrange
        System.Reflection.Assembly assembly = typeof(JsonLocalizationDictionaryBuilderTests).Assembly;
        const string prefix = "Granit.Localization.Tests.TestResources.Localization.Parent";

        // Act
        Dictionary<string, Dictionary<string, string>> result =
            Internal.JsonLocalizationDictionaryBuilder.Build(assembly, prefix);

        // Assert
        result.ShouldContainKey("fr");
        result["fr"].ShouldContainKey("Parent:SharedKey");
        result["fr"]["Parent:SharedKey"].ShouldBe("Valeur partagée du parent");
    }
}
