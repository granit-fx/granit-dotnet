using Granit.Http.ApiDocumentation.Attributes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Removes from the OpenAPI document all paths whose controller, action, or endpoint
/// is decorated with <see cref="InternalApiAttribute"/>.
/// Supports both MVC controllers and Wolverine HTTP endpoints.
/// </summary>
internal sealed class InternalApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> internalPaths = context.DescriptionGroups
            .SelectMany(g => g.Items)
            .Where(IsInternal)
            .Select(d => d.RelativePath ?? string.Empty)
            .Distinct();

        foreach (string path in internalPaths)
        {
            document.Paths.Remove("/" + path.TrimStart('/'));
        }

        return Task.CompletedTask;
    }

    private static bool IsInternal(ApiDescription description)
    {
        // Check EndpointMetadata first — works for both MVC and Wolverine endpoints.
        IList<object> endpointMetadata = description.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata.OfType<InternalApiAttribute>().Any())
        {
            return true;
        }

        // Fallback: check MVC-specific reflection metadata for method-level and class-level attributes.
        if (description.ActionDescriptor is ControllerActionDescriptor actionDescriptor)
        {
            return actionDescriptor.MethodInfo
                       .GetCustomAttributes(typeof(InternalApiAttribute), inherit: true)
                       .Length > 0
                   || actionDescriptor.ControllerTypeInfo
                       .GetCustomAttributes(typeof(InternalApiAttribute), inherit: true)
                       .Length > 0;
        }

        return false;
    }
}
