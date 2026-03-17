// =============================================================================
// Tests - ParameterDescriptionOperationTransformer
// =============================================================================
// Vérifie que les descriptions centralisées sont appliquées aux paramètres
// well-known, et que les descriptions existantes ne sont pas écrasées.
// =============================================================================

using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ParameterDescriptionOperationTransformerTests
{
    // --- Known parameter gets description ---

    [Theory]
    [InlineData("userId")]
    [InlineData("roleName")]
    [InlineData("entityType")]
    [InlineData("entityId")]
    [InlineData("id")]
    [InlineData("jobId")]
    [InlineData("typeName")]
    public async Task TransformAsync_KnownParameter_DescriptionSet(string parameterName)
    {
        // Arrange
        ParameterDescriptionOperationTransformer transformer = new();
        OpenApiOperation operation = BuildOperation(parameterName, description: null);

        // Act
        await transformer.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        operation.Parameters![0].Description.ShouldNotBeNullOrEmpty();
    }

    // --- Case insensitive matching ---

    [Fact]
    public async Task TransformAsync_CaseInsensitiveMatch_DescriptionSet()
    {
        // Arrange
        ParameterDescriptionOperationTransformer transformer = new();
        OpenApiOperation operation = BuildOperation("USERID", description: null);

        // Act
        await transformer.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        operation.Parameters![0].Description.ShouldNotBeNullOrEmpty();
    }

    // --- Existing description not overwritten ---

    [Fact]
    public async Task TransformAsync_ExistingDescription_NotOverwritten()
    {
        // Arrange
        ParameterDescriptionOperationTransformer transformer = new();
        OpenApiOperation operation = BuildOperation("userId", description: "Custom description.");

        // Act
        await transformer.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        operation.Parameters![0].Description.ShouldBe("Custom description.");
    }

    // --- Unknown parameter → no description ---

    [Fact]
    public async Task TransformAsync_UnknownParameter_DescriptionUnchanged()
    {
        // Arrange
        ParameterDescriptionOperationTransformer transformer = new();
        OpenApiOperation operation = BuildOperation("fooBar", description: null);

        // Act
        await transformer.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        operation.Parameters![0].Description.ShouldBeNull();
    }

    // --- Null parameters → no crash ---

    [Fact]
    public async Task TransformAsync_NullParameters_DoesNotThrow()
    {
        // Arrange
        ParameterDescriptionOperationTransformer transformer = new();
        OpenApiOperation operation = new() { Parameters = null };

        // Act & Assert
        await Should.NotThrowAsync(() =>
            transformer.TransformAsync(operation, BuildContext(), TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private static OpenApiOperation BuildOperation(string name, string? description) =>
        new()
        {
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = name,
                    In = ParameterLocation.Path,
                    Description = description,
                },
            ],
        };

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
