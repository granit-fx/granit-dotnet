using System.Net.Http;
using Granit.Http.ApiDocumentation.Options;
using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class OAuth2SecuritySchemeTransformerTests
{
    private const string AuthorizationUrl = "https://keycloak.example.com/realms/test/protocol/openid-connect/auth";
    private const string TokenUrl = "https://keycloak.example.com/realms/test/protocol/openid-connect/token";
    private const string ClientId = "test-frontend";

    [Fact]
    public async Task TransformAsync_NoJwtBearerScheme_DocumentUnchanged()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: false,
            oauth2Configured: true);
        OpenApiDocument document = new() { Paths = [] };
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components.ShouldBeNull("no JWT Bearer scheme → no-op");
    }

    [Fact]
    public async Task TransformAsync_OAuth2NotConfigured_DocumentUnchanged()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: false);

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components!.SecuritySchemes!.ShouldContainKey("Bearer");
        document.Components!.SecuritySchemes!.ShouldNotContainKey("OAuth2");
    }

    [Fact]
    public async Task TransformAsync_OAuth2Configured_ReplacesWithOAuth2Scheme()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true);

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components!.SecuritySchemes!.ShouldContainKey("OAuth2");
        var scheme = (OpenApiSecurityScheme)document.Components!.SecuritySchemes!["OAuth2"];
        scheme.Type.ShouldBe(SecuritySchemeType.OAuth2);
    }

    [Fact]
    public async Task TransformAsync_OAuth2Configured_BearerSchemeRemoved()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true);

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        document.Components!.SecuritySchemes!.ShouldNotContainKey("Bearer");
    }

    [Fact]
    public async Task TransformAsync_OAuth2Configured_AuthorizationCodeFlowPopulated()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true);

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        var scheme = (OpenApiSecurityScheme)document.Components!.SecuritySchemes!["OAuth2"];
        OpenApiOAuthFlow flow = scheme.Flows!.AuthorizationCode!;
        flow.AuthorizationUrl.ShouldBe(new Uri(AuthorizationUrl));
        flow.TokenUrl.ShouldBe(new Uri(TokenUrl));
    }

    [Fact]
    public async Task TransformAsync_OAuth2Configured_ScopesPopulated()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true,
            scopes: ["openid", "profile"]);

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        var scheme = (OpenApiSecurityScheme)document.Components!.SecuritySchemes!["OAuth2"];
        OpenApiOAuthFlow flow = scheme.Flows!.AuthorizationCode!;
        flow.Scopes.ShouldNotBeNull();
        flow.Scopes!.ShouldContainKey("openid");
        flow.Scopes!.ShouldContainKey("profile");
    }

    [Fact]
    public async Task TransformAsync_OAuth2Configured_SecurityRequirementsOnOperations()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true);

        OpenApiOperation operation = new() { Summary = "Get items" };
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", null, null)] = [],
        }];

        OpenApiPathItem pathItem = new()
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = operation,
            },
        };

        OpenApiDocument document = BuildDocumentWithBearerScheme();
        document.Paths["/api/v1/items"] = pathItem;

        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        operation.Security.ShouldNotBeNull();
        operation.Security.Count.ShouldBe(1);
        OpenApiSecurityRequirement requirement = operation.Security[0];
        requirement.ShouldNotBeEmpty();
        IOpenApiSecurityScheme key = requirement.Keys.First();
        key.ShouldBeOfType<OpenApiSecuritySchemeReference>();
    }

    [Fact]
    public async Task TransformAsync_PathsWithNullOperations_DoesNotThrow()
    {
        // Arrange
        OAuth2SecuritySchemeTransformer transformer = BuildTransformer(
            hasBearer: true,
            oauth2Configured: true);

        OpenApiPathItem pathItemWithNullOps = new() { Operations = null };
        OpenApiDocument document = BuildDocumentWithBearerScheme();
        document.Paths["/api/v1/items"] = pathItemWithNullOps;

        OpenApiDocumentTransformerContext context = BuildContext();

        // Act
        Func<Task> act = () => transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }

    private static OAuth2SecuritySchemeTransformer BuildTransformer(
        bool hasBearer,
        bool oauth2Configured,
        IList<string>? scopes = null)
    {
        IAuthenticationSchemeProvider provider = Substitute.For<IAuthenticationSchemeProvider>();
        if (hasBearer)
        {
            AuthenticationScheme bearerScheme = new(
                "Bearer",
                displayName: null,
                handlerType: typeof(StubAuthHandler));
            provider.GetAllSchemesAsync().Returns([bearerScheme]);
        }
        else
        {
            provider.GetAllSchemesAsync().Returns([]);
        }

        ApiDocumentationOptions apiDocOptions = new();
        if (oauth2Configured)
        {
            apiDocOptions.OAuth2 = new OAuth2Options
            {
                AuthorizationUrl = AuthorizationUrl,
                TokenUrl = TokenUrl,
                ClientId = ClientId,
                Scopes = scopes ?? ["openid"],
            };
        }

        IOptions<ApiDocumentationOptions> options = Microsoft.Extensions.Options.Options.Create(apiDocOptions);
        return new OAuth2SecuritySchemeTransformer(provider, options);
    }

    private static OpenApiDocument BuildDocumentWithBearerScheme() =>
        new()
        {
            Paths = [],
            Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                    },
                },
            },
        };

    private static OpenApiDocumentTransformerContext BuildContext() =>
        new()
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };

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
