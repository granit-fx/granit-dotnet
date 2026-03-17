using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Registers the shared <c>ProblemDetails</c> schema in <c>components/schemas</c> so that
/// error responses can reference it via <c>$ref</c> instead of inlining the same object on every operation.
/// </summary>
internal sealed class ProblemDetailsSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        document.Components.Schemas[ProblemDetailsResponseOperationTransformer.SchemaName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference that identifies the problem type." },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A short, human-readable summary of the problem type." },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "The HTTP status code." },
                ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A human-readable explanation specific to this occurrence." },
                ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference that identifies the specific occurrence." },
            },
        };

        return Task.CompletedTask;
    }
}
