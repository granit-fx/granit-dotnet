// =============================================================================
// Tests - SingleValueObjectSchemaTransformer
// =============================================================================
// Vérifie que les schémas des SingleValueObject<T> sont réécrits vers le
// schéma du primitif T (format wire de SingleValueObjectJsonConverterFactory)
// et que les types non-VO passent inchangés.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class SingleValueObjectSchemaTransformerTests
{
    private sealed class GuidValueObject : SingleValueObject<Guid>
    {
        public override required Guid Value { get; init; }
    }

    private sealed class IntValueObject : SingleValueObject<int>
    {
        public override required int Value { get; init; }
    }

    private sealed class LongValueObject : SingleValueObject<long>
    {
        public override required long Value { get; init; }
    }

    private sealed class DecimalValueObject : SingleValueObject<decimal>
    {
        public override required decimal Value { get; init; }
    }

    private sealed class BoolValueObject : SingleValueObject<bool>
    {
        public override required bool Value { get; init; }
    }

    private sealed record PlainRequest(string Name);

    // --- Concrete framework value objects (all string-backed) ---

    [Theory]
    [InlineData(typeof(ContentType))]
    [InlineData(typeof(HexColor))]
    [InlineData(typeof(HttpsUrl))]
    [InlineData(typeof(AbsoluteUrl))]
    [InlineData(typeof(FileName))]
    [InlineData(typeof(EntityTypeName))]
    [InlineData(typeof(BlobReference))]
    public async Task FrameworkStringValueObjects_RewriteToStringSchema(Type valueObjectType)
    {
        // Arrange — the generator emits an object schema with a "value" property for VOs.
        SingleValueObjectSchemaTransformer transformer = new();
        OpenApiSchema schema = BuildObjectSchemaWithValueProperty();
        OpenApiSchemaTransformerContext context = BuildContext(valueObjectType);

        // Act
        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        // Assert — plain string on the wire ("text/html", "#ff0000", …)
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Format.ShouldBeNull();
        schema.Properties.ShouldBeNull();
        schema.Required.ShouldBeNull();
    }

    // --- Non-string primitives keep their type/format pair ---

    [Theory]
    [InlineData(typeof(GuidValueObject), JsonSchemaType.String, "uuid")]
    [InlineData(typeof(IntValueObject), JsonSchemaType.Integer, "int32")]
    [InlineData(typeof(LongValueObject), JsonSchemaType.Integer, "int64")]
    [InlineData(typeof(DecimalValueObject), JsonSchemaType.Number, "double")]
    [InlineData(typeof(BoolValueObject), JsonSchemaType.Boolean, null)]
    public async Task PrimitiveBackedValueObjects_MapTypeAndFormat(
        Type valueObjectType, JsonSchemaType expectedType, string? expectedFormat)
    {
        SingleValueObjectSchemaTransformer transformer = new();
        OpenApiSchema schema = BuildObjectSchemaWithValueProperty();
        OpenApiSchemaTransformerContext context = BuildContext(valueObjectType);

        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        schema.Type.ShouldBe(expectedType);
        schema.Format.ShouldBe(expectedFormat);
        schema.Properties.ShouldBeNull();
    }

    // --- Non-VO types pass through untouched ---

    [Fact]
    public async Task NonValueObjectType_PassesThroughUntouched()
    {
        SingleValueObjectSchemaTransformer transformer = new();
        OpenApiSchema schema = BuildObjectSchemaWithValueProperty();
        OpenApiSchemaTransformerContext context = BuildContext(typeof(PlainRequest));

        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        schema.Type.ShouldBe(JsonSchemaType.Object);
        schema.Properties.ShouldNotBeNull();
        schema.Properties!.ShouldContainKey("value");
    }

    [Fact]
    public async Task PlainString_PassesThroughUntouched()
    {
        SingleValueObjectSchemaTransformer transformer = new();
        OpenApiSchema schema = new() { Type = JsonSchemaType.String, Format = "email" };
        OpenApiSchemaTransformerContext context = BuildContext(typeof(string));

        await transformer.TransformAsync(schema, context, TestContext.Current.CancellationToken);

        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Format.ShouldBe("email");
    }

    // --- Helpers ---

    private static OpenApiSchema BuildObjectSchemaWithValueProperty() =>
        new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["value"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            Required = new HashSet<string> { "value" },
        };

    private static OpenApiSchemaTransformerContext BuildContext(Type type)
    {
        JsonTypeInfo typeInfo = JsonSerializerOptions.Default.GetTypeInfo(type);
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
