// =============================================================================
// Tests - SchemaExampleSchemaTransformer
// =============================================================================
// Vérifie que le transformer applique les exemples JSON aux schémas OpenAPI
// en collectant les exemples des ISchemaExampleProvider enregistrés.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class SchemaExampleSchemaTransformerTests
{
    private sealed record SampleRequest(string Name, int Age);

    private sealed record OtherRequest(string Value);

    // --- Matching type gets example applied ---

    [Fact]
    public async Task TransformAsync_MatchingType_SetsExample()
    {
        // Arrange
        JsonObject example = new() { ["name"] = "Alice", ["age"] = 30 };
        ISchemaExampleProvider provider = CreateProvider(typeof(SampleRequest), example);
        SchemaExampleSchemaTransformer transformer = new([provider]);

        OpenApiSchema schema = new();
        OpenApiSchemaTransformerContext context = BuildContext<SampleRequest>();

        // Act
        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        // Assert
        schema.Example.ShouldNotBeNull();
        JsonNode exampleNode = schema.Example;
        exampleNode["name"]!.GetValue<string>().ShouldBe("Alice");
        exampleNode["age"]!.GetValue<int>().ShouldBe(30);
    }

    // --- Non-matching type leaves schema unchanged ---

    [Fact]
    public async Task TransformAsync_NonMatchingType_LeavesExampleNull()
    {
        // Arrange
        JsonObject example = new() { ["name"] = "Alice" };
        ISchemaExampleProvider provider = CreateProvider(typeof(SampleRequest), example);
        SchemaExampleSchemaTransformer transformer = new([provider]);

        OpenApiSchema schema = new();
        OpenApiSchemaTransformerContext context = BuildContext<OtherRequest>();

        // Act
        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        // Assert
        schema.Example.ShouldBeNull();
    }

    // --- Example is deep-cloned (mutation-safe) ---

    [Fact]
    public async Task TransformAsync_ReturnsDeepClone_NotSameReference()
    {
        // Arrange
        JsonObject example = new() { ["value"] = "original" };
        ISchemaExampleProvider provider = CreateProvider(typeof(SampleRequest), example);
        SchemaExampleSchemaTransformer transformer = new([provider]);

        OpenApiSchema schema1 = new();
        OpenApiSchema schema2 = new();
        OpenApiSchemaTransformerContext context = BuildContext<SampleRequest>();

        // Act
        await transformer.TransformAsync(schema1, context, TestContext.Current.CancellationToken);
        await transformer.TransformAsync(schema2, context, TestContext.Current.CancellationToken);

        // Assert — different references
        ReferenceEquals(schema1.Example, schema2.Example).ShouldBeFalse();
    }

    // --- Multiple providers are merged ---

    [Fact]
    public async Task TransformAsync_MultipleProviders_MergesExamples()
    {
        // Arrange
        ISchemaExampleProvider provider1 = CreateProvider(
            typeof(SampleRequest), new JsonObject { ["name"] = "Alice" });
        ISchemaExampleProvider provider2 = CreateProvider(
            typeof(OtherRequest), new JsonObject { ["value"] = "test" });

        SchemaExampleSchemaTransformer transformer = new([provider1, provider2]);

        OpenApiSchema schema1 = new();
        OpenApiSchema schema2 = new();

        // Act
        await transformer.TransformAsync(schema1, BuildContext<SampleRequest>(), TestContext.Current.CancellationToken);
        await transformer.TransformAsync(schema2, BuildContext<OtherRequest>(), TestContext.Current.CancellationToken);

        // Assert
        schema1.Example.ShouldNotBeNull();
        schema1.Example["name"]!.GetValue<string>().ShouldBe("Alice");
        schema2.Example.ShouldNotBeNull();
        schema2.Example["value"]!.GetValue<string>().ShouldBe("test");
    }

    // --- First provider wins on duplicate type ---

    [Fact]
    public async Task TransformAsync_DuplicateType_FirstProviderWins()
    {
        // Arrange
        ISchemaExampleProvider provider1 = CreateProvider(
            typeof(SampleRequest), new JsonObject { ["name"] = "First" });
        ISchemaExampleProvider provider2 = CreateProvider(
            typeof(SampleRequest), new JsonObject { ["name"] = "Second" });

        SchemaExampleSchemaTransformer transformer = new([provider1, provider2]);

        OpenApiSchema schema = new();

        // Act
        await transformer.TransformAsync(schema, BuildContext<SampleRequest>(), TestContext.Current.CancellationToken);

        // Assert
        schema.Example!["name"]!.GetValue<string>().ShouldBe("First");
    }

    // --- Empty providers → no crash ---

    [Fact]
    public async Task TransformAsync_NoProviders_DoesNotThrow()
    {
        // Arrange
        SchemaExampleSchemaTransformer transformer = new([]);
        OpenApiSchema schema = new();

        // Act & Assert
        await Should.NotThrowAsync(() =>
            transformer.TransformAsync(schema, BuildContext<SampleRequest>(), TestContext.Current.CancellationToken));
        schema.Example.ShouldBeNull();
    }

    // --- Helpers ---

    private static ISchemaExampleProvider CreateProvider(Type type, JsonNode example)
    {
        ISchemaExampleProvider provider = Substitute.For<ISchemaExampleProvider>();
        provider.GetExamples().Returns(new Dictionary<Type, JsonNode> { [type] = example });
        return provider;
    }

    private static OpenApiSchemaTransformerContext BuildContext<T>()
    {
        JsonTypeInfo typeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(T));
        return new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            JsonTypeInfo = typeInfo,
            JsonPropertyInfo = null!,
            ParameterDescription = null!,
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
    }
}
