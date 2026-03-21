using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class DictionarySchemaExampleOperationTransformerTests
{
    private readonly DictionarySchemaExampleOperationTransformer _sut = new();

    [Fact]
    public async Task TransformAsync_NullResponses_DoesNotThrow()
    {
        OpenApiOperation operation = new() { Responses = null };

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        operation.Responses.ShouldBeNull();
    }

    [Fact]
    public async Task TransformAsync_EmptyResponses_DoesNotThrow()
    {
        OpenApiOperation operation = new() { Responses = new OpenApiResponses() };

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        operation.Responses.ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_DictionarySchema_StringValues_AddsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldNotBeNull();
        JsonObject example = schema.Example.ShouldBeOfType<JsonObject>();
        example["key1"].ShouldNotBeNull();
        example["key2"].ShouldNotBeNull();
    }

    [Fact]
    public async Task TransformAsync_DictionarySchema_IntegerValues_AddsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Integer },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldNotBeNull();
    }

    [Fact]
    public async Task TransformAsync_DictionarySchema_NumberValues_AddsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Number },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldNotBeNull();
    }

    [Fact]
    public async Task TransformAsync_DictionarySchema_BooleanValues_AddsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Boolean },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldNotBeNull();
    }

    [Fact]
    public async Task TransformAsync_SchemaWithExistingExample_DoesNotOverwrite()
    {
        JsonObject existingExample = new() { ["existing"] = "value" };
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
            Example = existingExample,
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldBeSameAs(existingExample);
    }

    [Fact]
    public async Task TransformAsync_SchemaWithNamedProperties_SkipsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldBeNull();
    }

    [Fact]
    public async Task TransformAsync_NonObjectSchema_SkipsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Array,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldBeNull();
    }

    [Fact]
    public async Task TransformAsync_NoAdditionalProperties_SkipsExample()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
        };

        OpenApiOperation operation = CreateOperationWithSchema(schema);

        await _sut.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        schema.Example.ShouldBeNull();
    }

    private static OpenApiOperation CreateOperationWithSchema(OpenApiSchema schema)
    {
        OpenApiResponse response = new()
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = schema },
            },
        };

        return new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = response,
            },
        };
    }

    private static OpenApiOperationTransformerContext BuildContext()
    {
        ActionDescriptor descriptor = new();
        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new ApiDescription
            {
                ActionDescriptor = descriptor,
                RelativePath = "api/test",
            },
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
    }
}
