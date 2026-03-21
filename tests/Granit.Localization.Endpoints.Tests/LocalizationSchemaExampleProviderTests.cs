using System.Text.Json.Nodes;
using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class LocalizationSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ContainsSetLocalizationOverrideRequest()
    {
        LocalizationSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(SetLocalizationOverrideRequest));
    }

    [Fact]
    public void GetExamples_SetLocalizationOverrideRequest_HasValueProperty()
    {
        LocalizationSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();
        JsonNode example = examples[typeof(SetLocalizationOverrideRequest)];

        example["value"].ShouldNotBeNull();
        example["value"]!.GetValue<string>().ShouldBe("Tableau de bord");
    }

    [Fact]
    public void GetExamples_ReturnsNonEmptyDictionary()
    {
        LocalizationSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldNotBeEmpty();
    }
}
