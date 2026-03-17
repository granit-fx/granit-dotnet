using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Adds the JWT Bearer security scheme definition and a global security requirement to the OpenAPI document
/// when JWT Bearer authentication is registered in the application.
/// No-op when JWT Bearer is not configured, preventing false security indicators on public APIs.
/// </summary>
/// <remarks>
/// Per-operation security is handled by <see cref="SecurityRequirementOperationTransformer"/>:
/// anonymous endpoints explicitly override the global requirement with an empty security entry,
/// while protected endpoints inherit the global requirement by having no per-operation override.
/// </remarks>
internal sealed class JwtBearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    private const string BearerSchemeId = "Bearer";

    /// <inheritdoc/>
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        IEnumerable<AuthenticationScheme> schemes =
            await authenticationSchemeProvider.GetAllSchemesAsync().ConfigureAwait(false);

        bool hasJwtBearer = schemes.Any(s => s.Name == BearerSchemeId);
        if (!hasJwtBearer)
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerSchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Bearer token. Example: \"Authorization: Bearer {token}\"",
            In = ParameterLocation.Header,
            Name = "Authorization",
        };

        OpenApiSecurityRequirement securityRequirement = new()
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeId, null, null)] = [],
        };

        document.Security ??= [];
        document.Security.Add(securityRequirement);
    }
}
