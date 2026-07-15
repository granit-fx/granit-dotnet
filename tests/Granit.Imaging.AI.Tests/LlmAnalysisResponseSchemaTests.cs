using System.Text.Json;
using Granit.Imaging.AI.Internal;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

/// <summary>
/// Locks the wire contract between <see cref="LlmAnalysisResponse"/> and the
/// structured-completion primitive: camelCase/case-insensitive deserialization
/// (the primitive's serializer settings) and JSON-schema generation must both work.
/// </summary>
public sealed class LlmAnalysisResponseSchemaTests
{
    private static readonly JsonSerializerOptions PrimitiveSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Deserializes_camelCase_json_like_the_primitive()
    {
        const string json = """
            {
                "description": "A red car",
                "detectedObjects": ["car"],
                "tags": ["outdoor"],
                "suggestedAltText": "Red car"
            }
            """;

        LlmAnalysisResponse? parsed = JsonSerializer.Deserialize<LlmAnalysisResponse>(json, PrimitiveSerializerOptions);

        parsed.ShouldNotBeNull();
        parsed.Description.ShouldBe("A red car");
        parsed.DetectedObjects.ShouldBe(["car"]);
        parsed.Tags.ShouldBe(["outdoor"]);
        parsed.SuggestedAltText.ShouldBe("Red car");
    }

    [Fact]
    public void Json_schema_generation_succeeds()
    {
        JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(LlmAnalysisResponse));

        schema.ValueKind.ShouldBe(JsonValueKind.Object);
        schema.GetProperty("properties").TryGetProperty("description", out _).ShouldBeTrue();
    }
}
