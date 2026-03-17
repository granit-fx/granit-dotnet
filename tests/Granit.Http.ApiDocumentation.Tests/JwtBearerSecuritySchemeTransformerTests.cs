// =============================================================================
// Tests - JwtBearerSecuritySchemeTransformer
// =============================================================================
// Vérifie que le transformer ajoute le schéma Bearer et les exigences de
// sécurité aux opérations quand JWT Bearer est configuré, et est no-op sinon.
// =============================================================================

using System.Net.Http;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class JwtBearerSecuritySchemeTransformerTests
{
    // --- No JWT Bearer scheme registered ---

    [Fact]
    public async Task TransformAsync_NoJwtBearerScheme_DocumentUnchanged()
    {
        // Arrange
        IAuthenticationSchemeProvider provider = Substitute.For<IAuthenticationSchemeProvider>();
        provider.GetAllSchemesAsync().Returns([]);

        JwtBearerSecuritySchemeTransformer transformer = new(provider);
        OpenApiDocument document = new() { Paths = [] };
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components.ShouldBeNull("no JWT Bearer scheme → no security scheme added");
    }

    // --- JWT Bearer scheme registered ---

    [Fact]
    public async Task TransformAsync_JwtBearerScheme_SecuritySchemeAdded()
    {
        // Arrange
        IAuthenticationSchemeProvider provider = BuildProviderWithBearer();
        JwtBearerSecuritySchemeTransformer transformer = new(provider);
        OpenApiDocument document = new() { Paths = [] };
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components.ShouldNotBeNull();
        document.Components!.SecuritySchemes!.ShouldContainKey("Bearer");
        var scheme = (OpenApiSecurityScheme)document.Components!.SecuritySchemes!["Bearer"];
        scheme.Type.ShouldBe(SecuritySchemeType.Http);
        scheme.Scheme.ShouldBe("bearer");
        scheme.BearerFormat.ShouldBe("JWT");
    }

    // --- Global security requirement added ---

    [Fact]
    public async Task TransformAsync_JwtBearerSchemeWithOperations_GlobalSecurityRequirementAdded()
    {
        // Arrange
        IAuthenticationSchemeProvider provider = BuildProviderWithBearer();
        JwtBearerSecuritySchemeTransformer transformer = new(provider);

        OpenApiOperation operation = new() { Summary = "Get items" };
        OpenApiPathItem pathItem = new();
        pathItem.Operations = new Dictionary<HttpMethod, OpenApiOperation>
        {
            [HttpMethod.Get] = operation,
        };

        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/api/v1/items"] = pathItem,
            },
        };

        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert — security is set at document level, not per-operation
        document.Security.ShouldNotBeNull();
        document.Security!.ShouldNotBeEmpty();
        operation.Security.ShouldBeNull("per-operation security is handled by SecurityRequirementOperationTransformer");
    }

    // --- Paths with null operations do not throw ---

    [Fact]
    public async Task TransformAsync_JwtBearerSchemeWithNullOperations_DoesNotThrow()
    {
        // Arrange
        IAuthenticationSchemeProvider provider = BuildProviderWithBearer();
        JwtBearerSecuritySchemeTransformer transformer = new(provider);

        OpenApiPathItem pathItemWithNullOps = new() { Operations = null };
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/api/v1/items"] = pathItemWithNullOps,
            },
        };

        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        Func<Task> act = () => transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }

    // --- Helpers ---

    private static IAuthenticationSchemeProvider BuildProviderWithBearer()
    {
        IAuthenticationSchemeProvider provider = Substitute.For<IAuthenticationSchemeProvider>();
        AuthenticationScheme bearerScheme = new(
            "Bearer",
            displayName: null,
            handlerType: typeof(StubAuthHandler));
        provider.GetAllSchemesAsync().Returns([bearerScheme]);
        return provider;
    }

    private static OpenApiDocumentTransformerContext BuildContext() =>
        new()
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };

    /// <summary>Minimal IAuthenticationHandler stub required by AuthenticationScheme constructor.</summary>
    private sealed class StubAuthHandler : IAuthenticationHandler
    {
        public Task<AuthenticateResult> AuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) =>
            Task.CompletedTask;
    }
}
