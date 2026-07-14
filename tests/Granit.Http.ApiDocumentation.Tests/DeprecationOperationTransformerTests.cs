// =============================================================================
// Tests - DeprecationOperationTransformer
// =============================================================================
// Vérifie que les opérations dont l'endpoint porte DeprecatedAttribute sont
// marquées deprecated: true dans le document OpenAPI.
// =============================================================================

using Granit.Http.ApiDocumentation.Deprecation;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class DeprecationOperationTransformerTests
{
    [Fact]
    public async Task DeprecatedAttribute_SetsOperationDeprecated()
    {
        // Arrange
        DeprecationOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = BuildContext(new DeprecatedAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Deprecated.ShouldBeTrue();
    }

    [Fact]
    public async Task NoMetadata_LeavesOperationUntouched()
    {
        // Arrange
        DeprecationOperationTransformer transformer = new();
        OpenApiOperation operation = new();
        OpenApiOperationTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Deprecated.ShouldBeFalse();
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
