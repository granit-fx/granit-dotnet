// =============================================================================
// Tests - InternalTypeSchemaDocumentTransformer
// =============================================================================
// Vérifie que les types internes .NET (IFormFile, JsonElement) sont supprimés
// du schéma components/schemas du document OpenAPI.
// =============================================================================

using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class InternalTypeSchemaDocumentTransformerTests
{
    // --- IFormFile removed ---

    [Fact]
    public async Task TransformAsync_IFormFileSchema_Removed()
    {
        // Arrange
        InternalTypeSchemaDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocumentWithSchemas("IFormFile", "MyDto");

        // Act
        await transformer.TransformAsync(document, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        document.Components!.Schemas!.ShouldNotContainKey("IFormFile");
        document.Components.Schemas!.ShouldContainKey("MyDto");
    }

    // --- JsonElement removed ---

    [Fact]
    public async Task TransformAsync_JsonElementSchema_Removed()
    {
        // Arrange
        InternalTypeSchemaDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocumentWithSchemas("JsonElement", "MyDto");

        // Act
        await transformer.TransformAsync(document, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        document.Components!.Schemas!.ShouldNotContainKey("JsonElement");
        document.Components.Schemas!.ShouldContainKey("MyDto");
    }

    // --- No schemas → no crash ---

    [Fact]
    public async Task TransformAsync_NullSchemas_DoesNotThrow()
    {
        // Arrange
        InternalTypeSchemaDocumentTransformer transformer = new();
        OpenApiDocument document = new();

        // Act & Assert
        await Should.NotThrowAsync(() =>
            transformer.TransformAsync(document, BuildContext(), TestContext.Current.CancellationToken));
    }

    // --- Non-internal types preserved ---

    [Fact]
    public async Task TransformAsync_NonInternalTypes_Preserved()
    {
        // Arrange
        InternalTypeSchemaDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocumentWithSchemas("SavedViewResponse", "UserNotificationResponse");

        // Act
        await transformer.TransformAsync(document, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        document.Components!.Schemas!.Count.ShouldBe(2);
    }

    // --- Helpers ---

    private static OpenApiDocument BuildDocumentWithSchemas(params string[] schemaNames)
    {
        OpenApiDocument document = new()
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(),
            },
        };

        foreach (string name in schemaNames)
        {
            document.Components.Schemas[name] = new OpenApiSchema { Type = JsonSchemaType.Object };
        }

        return document;
    }

    private static OpenApiDocumentTransformerContext BuildContext() =>
        new()
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
}
