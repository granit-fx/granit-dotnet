// =============================================================================
// Tests - BinaryResponseContentTypeOperationTransformer
// =============================================================================
// Verifies that content-type entries are injected for responses where
// .Produces(statusCode, contentType: "...") was called without a CLR type.
// =============================================================================

using Granit.Http.ApiDocumentation.Transformers.Compatibility;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class BinaryResponseContentTypeOperationTransformerTests
{
    // --- Binary content type (octet-stream) ---

    [Fact]
    public async Task TransformAsync_OctetStreamWithoutType_InjectsBinarySchema()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, ["application/octet-stream"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content!.ShouldContainKey("application/octet-stream");
        var mediaType = (OpenApiMediaType)response.Content!["application/octet-stream"];
        var schema = (OpenApiSchema)mediaType.Schema!;
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Format.ShouldBe("binary");
    }

    // --- Zip content type ---

    [Fact]
    public async Task TransformAsync_ZipContentTypeWithoutType_InjectsBinarySchema()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, ["application/zip"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content!.ShouldContainKey("application/zip");
        var mediaType = (OpenApiMediaType)response.Content!["application/zip"];
        var schema = (OpenApiSchema)mediaType.Schema!;
        schema.Type.ShouldBe(JsonSchemaType.String);
        schema.Format.ShouldBe("binary");
    }

    // --- application/json without CLR type → empty media type (no schema) ---

    [Fact]
    public async Task TransformAsync_UntypedJsonContentType_InjectsEmptyMediaType()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, ["application/json"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content!.ShouldContainKey("application/json");
        var mediaType = (OpenApiMediaType)response.Content!["application/json"];
        mediaType.Schema.ShouldBeNull();
    }

    // --- Typed response — must not be touched (native generator handles it) ---

    [Fact]
    public async Task TransformAsync_TypedResponse_DoesNotModifyContent()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, typeof(string), ["application/json"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert — content untouched; native generator owns typed responses
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content.ShouldBeNull();
    }

    // --- No content types declared → no-op ---

    [Fact]
    public async Task TransformAsync_NoContentTypes_DoesNotModifyContent()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, []);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content.ShouldBeNull();
    }

    // --- Already populated content → must not be overwritten ---

    [Fact]
    public async Task TransformAsync_ExistingContent_DoesNotOverwrite()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiMediaType existing = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } };
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "OK",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = existing,
                    },
                },
            },
        };

        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, ["application/json"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert — existing entry preserved
        var response = (OpenApiResponse)operation.Responses["200"];
        response.Content!["application/json"].ShouldBeSameAs(existing);
    }

    // --- Status code mismatch → no-op ---

    [Fact]
    public async Task TransformAsync_StatusCodeMismatch_DoesNotModifyOtherResponse()
    {
        // Arrange
        BinaryResponseContentTypeOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["201"] = new OpenApiResponse { Description = "Created" },
            },
        };

        // Metadata declares 200, operation only has 201
        IProducesResponseTypeMetadata metadata = BuildProducesMetadata(200, null, ["application/octet-stream"]);
        OpenApiOperationTransformerContext context = BuildContext(metadata);

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        var response = (OpenApiResponse)operation.Responses["201"];
        response.Content.ShouldBeNull();
    }

    // --- Helpers ---

    private static IProducesResponseTypeMetadata BuildProducesMetadata(
        int statusCode,
        Type? type,
        IEnumerable<string> contentTypes)
    {
        IProducesResponseTypeMetadata metadata = Substitute.For<IProducesResponseTypeMetadata>();
        metadata.StatusCode.Returns(statusCode);
        metadata.Type.Returns(type);
        metadata.ContentTypes.Returns(contentTypes);
        return metadata;
    }

    private static OpenApiOperationTransformerContext BuildContext(params object[] metadata)
    {
        ActionDescriptor descriptor = new();
        descriptor.EndpointMetadata = [.. metadata];

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
