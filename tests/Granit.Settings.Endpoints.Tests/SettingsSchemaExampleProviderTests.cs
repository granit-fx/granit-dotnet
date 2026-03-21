using System.Text.Json.Nodes;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsSchemaExampleProviderTests
{
    [Fact]
    public void GetExamples_ContainsUpdateSettingValueRequest()
    {
        SettingsSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(UpdateSettingValueRequest));
    }

    [Fact]
    public void GetExamples_ContainsSettingValueResponse()
    {
        SettingsSchemaExampleProvider provider = new();

        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        examples.ShouldContainKey(typeof(SettingValueResponse));
    }

    [Fact]
    public void UpdateSettingValueRequest_Example_HasValueProperty()
    {
        SettingsSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode example = examples[typeof(UpdateSettingValueRequest)];

        example["value"].ShouldNotBeNull();
        example["value"]!.GetValue<string>().ShouldBe("Europe/Brussels");
    }

    [Fact]
    public void SettingValueResponse_Example_HasNameAndValueProperties()
    {
        SettingsSchemaExampleProvider provider = new();
        IReadOnlyDictionary<Type, JsonNode> examples = provider.GetExamples();

        JsonNode example = examples[typeof(SettingValueResponse)];

        example["name"].ShouldNotBeNull();
        example["value"].ShouldNotBeNull();
    }
}
