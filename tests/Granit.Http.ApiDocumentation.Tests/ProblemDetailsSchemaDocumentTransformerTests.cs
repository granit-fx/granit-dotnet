using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ProblemDetailsSchemaDocumentTransformerTests
{
    private readonly ProblemDetailsSchemaDocumentTransformer _sut = new();

    [Fact]
    public async Task TransformAsync_AddsSchemaToComponents()
    {
        OpenApiDocument document = new();
        OpenApiDocumentTransformerContext context = BuildDocContext();

        await _sut.TransformAsync(document, context, CancellationToken.None);

        document.Components.ShouldNotBeNull();
        document.Components!.Schemas.ShouldNotBeNull();
        document.Components!.Schemas!.ShouldContainKey(ProblemDetailsResponseOperationTransformer.SchemaName);
    }

    [Fact]
    public async Task TransformAsync_SchemaHasExpectedProperties()
    {
        OpenApiDocument document = new();
        OpenApiDocumentTransformerContext context = BuildDocContext();

        await _sut.TransformAsync(document, context, CancellationToken.None);

        IOpenApiSchema schema = document.Components!.Schemas![ProblemDetailsResponseOperationTransformer.SchemaName];
        OpenApiSchema typedSchema = schema.ShouldBeOfType<OpenApiSchema>();
        typedSchema.Type.ShouldBe(JsonSchemaType.Object);
        typedSchema.Properties.ShouldNotBeNull();
        typedSchema.Properties!.ShouldContainKey("type");
        typedSchema.Properties!.ShouldContainKey("title");
        typedSchema.Properties!.ShouldContainKey("status");
        typedSchema.Properties!.ShouldContainKey("detail");
        typedSchema.Properties!.ShouldContainKey("instance");
    }

    [Fact]
    public async Task TransformAsync_ExistingComponents_PreservesAndAdds()
    {
        OpenApiDocument document = new()
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["ExistingType"] = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
        };
        OpenApiDocumentTransformerContext context = BuildDocContext();

        await _sut.TransformAsync(document, context, CancellationToken.None);

        document.Components.Schemas.ShouldContainKey("ExistingType");
        document.Components.Schemas.ShouldContainKey(ProblemDetailsResponseOperationTransformer.SchemaName);
    }

    [Fact]
    public async Task TransformAsync_NullComponents_CreatesComponentsAndSchemas()
    {
        OpenApiDocument document = new();
        OpenApiDocumentTransformerContext context = BuildDocContext();

        await _sut.TransformAsync(document, context, CancellationToken.None);

        document.Components.ShouldNotBeNull();
        document.Components.Schemas.ShouldNotBeNull();
    }

    private static OpenApiDocumentTransformerContext BuildDocContext() =>
        new()
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
}
