using Granit.Http.ApiDocumentation.Deprecation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Sets <c>deprecated: true</c> on operations whose endpoint metadata carries
/// <see cref="DeprecatedAttribute"/>, keeping the OpenAPI document in sync with
/// the RFC 8594 response headers emitted at runtime.
/// </summary>
internal sealed class DeprecationOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        bool isDeprecated = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<DeprecatedAttribute>()
            .Any();

        if (isDeprecated)
        {
            operation.Deprecated = true;
        }

        return Task.CompletedTask;
    }
}
