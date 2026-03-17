using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Removes .NET internal types (<c>IFormFile</c>, <c>JsonElement</c>) that leak into
/// <c>components/schemas</c> as artifacts of ASP.NET Core model binding.
/// These types are implementation details and should not appear in the public API contract.
/// </summary>
internal sealed class InternalTypeSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
    private static readonly HashSet<string> s_internalTypes =
    [
        "IFormFile",
        "JsonElement",
    ];

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is null)
        {
            return Task.CompletedTask;
        }

        foreach (string typeName in s_internalTypes)
        {
            document.Components.Schemas.Remove(typeName);
        }

        return Task.CompletedTask;
    }
}
