// =============================================================================
// Tests - InternalApiDocumentTransformer
// =============================================================================
// Vérifie que les paths dont le contrôleur ou l'action est marqué [InternalApi]
// sont retirés du document OpenAPI, et que les paths publics sont conservés.
// =============================================================================

using System.Reflection;
using Granit.Http.ApiDocumentation.Attributes;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class InternalApiDocumentTransformerTests
{
    // --- Controller marked [InternalApi] → path removed ---

    [Fact]
    public async Task TransformAsync_InternalController_PathRemoved()
    {
        // Arrange
        InternalApiDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocument("/api/v1/internal");
        OpenApiDocumentTransformerContext context = BuildContext(
            controllerType: typeof(InternalController),
            actionName: nameof(InternalController.Action),
            relativePath: "api/v1/internal");

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Paths.ShouldNotContainKey("/api/v1/internal");
    }

    // --- Action marked [InternalApi] → path removed ---

    [Fact]
    public async Task TransformAsync_InternalAction_PathRemoved()
    {
        // Arrange
        InternalApiDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocument("/api/v1/action");
        OpenApiDocumentTransformerContext context = BuildContext(
            controllerType: typeof(PublicControllerWithInternalAction),
            actionName: nameof(PublicControllerWithInternalAction.InternalAction),
            relativePath: "api/v1/action");

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Paths.ShouldNotContainKey("/api/v1/action");
    }

    // --- No [InternalApi] → path kept ---

    [Fact]
    public async Task TransformAsync_PublicController_PathKept()
    {
        // Arrange
        InternalApiDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocument("/api/v1/public");
        OpenApiDocumentTransformerContext context = BuildContext(
            controllerType: typeof(PublicController),
            actionName: nameof(PublicController.Action),
            relativePath: "api/v1/public");

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Paths.ShouldContainKey("/api/v1/public");
    }

    // --- Non-ControllerActionDescriptor without [InternalApi] → path kept ---

    [Fact]
    public async Task TransformAsync_NonControllerDescriptor_PathKept()
    {
        // Arrange
        InternalApiDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocument("/api/v1/minimal");

        Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor nonControllerDescriptor = new();
        ApiDescription apiDesc = new()
        {
            ActionDescriptor = nonControllerDescriptor,
            RelativePath = "api/v1/minimal",
        };

        OpenApiDocumentTransformerContext context = new()
        {
            DocumentName = "v1",
            DescriptionGroups = [new ApiDescriptionGroup("v1", [apiDesc])],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Paths.ShouldContainKey("/api/v1/minimal");
    }

    // --- Non-ControllerActionDescriptor with [InternalApi] in EndpointMetadata → path removed ---
    // Covers Wolverine HTTP endpoints where attributes are in EndpointMetadata, not on a ControllerActionDescriptor.

    [Fact]
    public async Task TransformAsync_WolverineEndpointWithInternalApiMetadata_PathRemoved()
    {
        // Arrange
        InternalApiDocumentTransformer transformer = new();
        OpenApiDocument document = BuildDocument("/api/v1/wolverine-internal");

        Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor descriptor = new();
        descriptor.EndpointMetadata = [new InternalApiAttribute()];

        ApiDescription apiDesc = new()
        {
            ActionDescriptor = descriptor,
            RelativePath = "api/v1/wolverine-internal",
        };

        OpenApiDocumentTransformerContext context = new()
        {
            DocumentName = "v1",
            DescriptionGroups = [new ApiDescriptionGroup("v1", [apiDesc])],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Paths.ShouldNotContainKey("/api/v1/wolverine-internal");
    }

    // --- Helpers ---

    private static OpenApiDocument BuildDocument(string path) =>
        new()
        {
            Paths = new OpenApiPaths
            {
                [path] = new OpenApiPathItem(),
            },
        };

    private static OpenApiDocumentTransformerContext BuildContext(
        Type controllerType,
        string actionName,
        string relativePath)
    {
        ControllerActionDescriptor descriptor = new()
        {
            MethodInfo = controllerType.GetMethod(actionName)!,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
        };

        ApiDescription apiDesc = new()
        {
            ActionDescriptor = descriptor,
            RelativePath = relativePath,
        };

        return new OpenApiDocumentTransformerContext
        {
            DocumentName = "v1",
            DescriptionGroups = [new ApiDescriptionGroup("v1", [apiDesc])],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
    }

    // --- Fake controllers ---

    [InternalApi]
    private sealed class InternalController
    {
        public static void Action() { }
    }

    private sealed class PublicController
    {
        public static void Action() { }
    }

    private sealed class PublicControllerWithInternalAction
    {
        [InternalApi]
        public static void InternalAction() { }
    }
}
