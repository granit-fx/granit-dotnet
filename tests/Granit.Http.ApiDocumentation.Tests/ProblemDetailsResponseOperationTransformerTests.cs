// =============================================================================
// Tests - ProblemDetailsResponseOperationTransformer
// =============================================================================
// Vérifie que les réponses d'erreur RFC 7807 sont ajoutées automatiquement
// et que les 404 fantômes de Wolverine sont nettoyés.
// =============================================================================

using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ProblemDetailsResponseOperationTransformerTests
{
    // --- Protected endpoint (GET with [Authorize]) → 401, 403, 500 ---

    [Fact]
    public async Task TransformAsync_ProtectedGetEndpoint_Adds401And403And500()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new() { Responses = [] };
        OpenApiOperationTransformerContext context = BuildContext(new AuthorizeAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldContainKey("401");
        operation.Responses.ShouldContainKey("403");
        operation.Responses.ShouldContainKey("500");
        operation.Responses.ShouldNotContainKey("422", "GET without body should not have 422");
    }

    // --- Protected POST with body → 401, 403, 422, 500 ---

    [Fact]
    public async Task TransformAsync_ProtectedPostWithBody_Adds422()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = [],
            RequestBody = new OpenApiRequestBody(),
        };
        OpenApiOperationTransformerContext context = BuildContext(new AuthorizeAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldContainKey("422");
        operation.Responses.ShouldContainKey("401");
        operation.Responses.ShouldContainKey("403");
        operation.Responses.ShouldContainKey("500");
    }

    // --- Public endpoint without route param → phantom 404 removed, 500 added ---

    [Fact]
    public async Task TransformAsync_PublicEndpointWithoutRouteParam_Removes404Phantom()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
                ["404"] = new OpenApiResponse { Description = "Not Found" },
            },
        };
        OpenApiOperationTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldNotContainKey("404");
        operation.Responses.ShouldContainKey("500");
        operation.Responses.ShouldNotContainKey("401", "no [Authorize] → no 401");
    }

    // --- Endpoint with route param {id} → 404 preserved ---

    [Fact]
    public async Task TransformAsync_EndpointWithRouteParam_Preserves404()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Parameters =
            [
                new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Required = true },
            ],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
                ["404"] = new OpenApiResponse { Description = "Not Found" },
            },
        };
        OpenApiOperationTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldContainKey("404", "endpoint with route parameter should keep 404");
        operation.Responses.ShouldContainKey("500");
    }

    // --- Does not overwrite existing responses ---

    [Fact]
    public async Task TransformAsync_ExistingResponse_NotOverwritten()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiResponse custom500 = new() { Description = "Custom error" };
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses { ["500"] = custom500 },
        };
        OpenApiOperationTransformerContext context = BuildContext(new AuthorizeAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses["500"].ShouldBeSameAs(custom500, "existing responses are not overwritten");
    }

    // --- [Authorize] + [AllowAnonymous] → not protected ---

    [Fact]
    public async Task TransformAsync_AuthorizeWithAllowAnonymous_NoAuthResponses()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new() { Responses = [] };
        OpenApiOperationTransformerContext context = BuildContext(
            new AuthorizeAttribute(), new AllowAnonymousAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldNotContainKey("401");
        operation.Responses.ShouldNotContainKey("403");
        operation.Responses.ShouldContainKey("500", "500 is always added");
    }

    // --- Error responses use $ref to shared ProblemDetails schema ---

    [Fact]
    public async Task TransformAsync_ErrorResponses_UseSchemaReference()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new() { Responses = [] };
        OpenApiOperationTransformerContext context = BuildContext(new AuthorizeAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert — all added responses reference the shared ProblemDetails schema
        AssertProblemDetailsRef(operation.Responses, "401");
        AssertProblemDetailsRef(operation.Responses, "403");
        AssertProblemDetailsRef(operation.Responses, "500");
    }

    // --- 404 without content gets enriched with ProblemDetails ---

    [Fact]
    public async Task TransformAsync_404WithoutContent_EnrichedWithProblemDetails()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Parameters =
            [
                new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Required = true },
            ],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "OK" },
                ["404"] = new OpenApiResponse { Description = "Not Found" },
            },
        };
        OpenApiOperationTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        AssertProblemDetailsRef(operation.Responses, "404");
    }

    // --- 422 skipped when 400 already exists (ASP.NET validation) ---

    [Fact]
    public async Task TransformAsync_RequestBodyWith400_Skips422()
    {
        // Arrange
        ProblemDetailsResponseOperationTransformer transformer = new();
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["400"] = new OpenApiResponse { Description = "Bad Request" },
            },
            RequestBody = new OpenApiRequestBody(),
        };
        OpenApiOperationTransformerContext context = BuildContext(new AuthorizeAttribute());

        // Act
        await transformer.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Responses.ShouldContainKey("400", "existing 400 is preserved");
        operation.Responses.ShouldNotContainKey("422", "422 is redundant when 400 exists");
    }

    // --- Document transformer registers shared schema ---

    [Fact]
    public async Task ProblemDetailsSchemaDocumentTransformer_RegistersSharedSchema()
    {
        // Arrange
        ProblemDetailsSchemaDocumentTransformer transformer = new();
        OpenApiDocument document = new();

        OpenApiDocumentTransformerContext context = new()
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components.ShouldNotBeNull();
        document.Components.Schemas.ShouldNotBeNull();
        document.Components.Schemas.ShouldContainKey("ProblemDetails");
    }

    // --- Helpers ---

    private static void AssertProblemDetailsRef(OpenApiResponses responses, string statusCode)
    {
        responses.ShouldContainKey(statusCode);
        var response = responses[statusCode] as OpenApiResponse;
        response.ShouldNotBeNull();
        response.Content.ShouldNotBeNull();
        response.Content.ShouldContainKey("application/problem+json");
        response.Content["application/problem+json"].Schema
            .ShouldBeOfType<OpenApiSchemaReference>()
            .Reference.Id.ShouldBe("ProblemDetails");
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
