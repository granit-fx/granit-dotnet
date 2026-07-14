using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers.Compatibility;

/// <summary>
/// Injects the declared media type into responses where <c>.Produces(statusCode, contentType: "…")</c>
/// was called without a CLR response type.
/// </summary>
/// <remarks>
/// ASP.NET Core's native OpenAPI generator only emits a <c>content</c> entry when a CLR schema is
/// associated with the response. Endpoints that stream binary files or return an untyped JSON body
/// declare the content type via <c>IProducesResponseTypeMetadata.ContentTypes</c> but no <c>Type</c>,
/// so the generator omits the <c>content</c> key entirely. This transformer fills the gap so
/// downstream code generators (Kiota, NSwag, etc.) can correctly map the response media type.
/// <para>
/// Binary content types (anything that is not <c>application/json</c>, <c>application/xml</c>,
/// or <c>text/*</c>) receive a <c>{ type: string, format: binary }</c> schema — the OpenAPI 3.x
/// standard representation for file downloads.
/// </para>
/// </remarks>
internal sealed class BinaryResponseContentTypeOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        foreach (IProducesResponseTypeMetadata producesMetadata in metadata.OfType<IProducesResponseTypeMetadata>())
        {
            // The native generator already handles entries with a CLR type (typed JSON responses).
            // Only process those where the developer explicitly declared a content type but no schema.
            bool hasType = producesMetadata.Type is not null && producesMetadata.Type != typeof(void);
            if (hasType)
            {
                continue;
            }

            IReadOnlyList<string> contentTypes = [.. producesMetadata.ContentTypes];
            if (contentTypes.Count == 0)
            {
                continue;
            }

            string statusCode = producesMetadata.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? openApiResponse)
                || openApiResponse is not OpenApiResponse response)
            {
                continue;
            }

            // Don't overwrite content the framework already emitted.
            if (response.Content is { Count: > 0 })
            {
                continue;
            }

            response.Content ??= new Dictionary<string, OpenApiMediaType>();

            foreach (string contentType in contentTypes)
            {
                response.Content[contentType] = BuildMediaType(contentType);
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiMediaType BuildMediaType(string contentType) =>
        IsBinaryContentType(contentType)
            ? new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
            : new OpenApiMediaType();

    private static bool IsBinaryContentType(string contentType) =>
        !contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        && !contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
        && !contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
        && !contentType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase);
}
