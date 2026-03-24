using Granit.Http.ApiDocumentation.Options;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Adds the tenant header as a required parameter on all operations whose endpoint
/// is not decorated with <see cref="AllowAnonymousTenantAttribute"/>.
/// Only active when <see cref="ApiDocumentationOptions.EnableTenantHeader"/> is <c>true</c>.
/// </summary>
internal sealed class TenantHeaderOperationTransformer(
    IOptions<ApiDocumentationOptions> options) : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ApiDocumentationOptions opts = options.Value;
        if (!opts.EnableTenantHeader)
        {
            return Task.CompletedTask;
        }

        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        bool isAnonymousTenant = metadata.OfType<AllowAnonymousTenantAttribute>().Any();
        if (isAnonymousTenant)
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = opts.TenantHeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Tenant identifier (UUID).",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        });

        return Task.CompletedTask;
    }
}
