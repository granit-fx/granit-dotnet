using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Normalizes per-operation security metadata so that the OpenAPI document correctly reflects
/// which endpoints require authentication and which are anonymous.
/// <list type="bullet">
///   <item><b>Anonymous endpoints</b> (<c>[AllowAnonymous]</c>): get <c>security: [{}]</c>
///     to explicitly override the global security requirement.</item>
///   <item><b>Protected endpoints</b>: get <c>security</c> cleared (<c>null</c>) so they
///     inherit the global Bearer/OAuth2 requirement set by document transformers.</item>
/// </list>
/// </summary>
/// <remarks>
/// Without this transformer, ASP.NET Core sets <c>security: [{}]</c> (empty requirement)
/// on all operations, which means "no authentication required" in OpenAPI, regardless of
/// the actual authorization policy.
/// </remarks>
internal sealed class SecurityRequirementOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool hasAllowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();

        if (hasAllowAnonymous)
        {
            // Override global security: no auth required for this operation.
            // An empty OpenApiSecurityRequirement serializes as {} meaning "no specific scheme".
            operation.Security = [[]];

        }
        else
        {
            // Remove per-operation security so the global document security applies.
            // In OpenAPI 3.1, absent operation-level security means "inherit global".
            operation.Security = null;
        }

        return Task.CompletedTask;
    }
}
