using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Normalizes nullable integer query parameters that ASP.NET Core generates as
/// <c>type: ["integer", "string"]</c> or <c>type: ["null", "integer"]</c> with a
/// spurious regex pattern, back to a clean <c>type: ["integer", "null"]</c>.
/// This is an artifact of model binding from query strings where <c>int?</c> parameters
/// accept both integer and string representations.
/// </summary>
internal sealed class NullableIntSchemaOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiParameter parameter in operation.Parameters)
        {
            if (parameter.Schema is not OpenApiSchema schema)
            {
                continue;
            }

            // Case 1: type: [integer, string] with int format → normalize to [integer, null]
            if (schema.Type == (JsonSchemaType.Integer | JsonSchemaType.String)
                && schema.Format is "int32" or "int64")
            {
                schema.Type = JsonSchemaType.Integer | JsonSchemaType.Null;
            }

            // Case 2: type: [null, integer] with regex pattern → remove spurious pattern
            if (schema.Type == (JsonSchemaType.Null | JsonSchemaType.Integer)
                && schema.Format is "int32" or "int64"
                && schema.Pattern is not null)
            {
                schema.Pattern = null;
            }
        }

        return Task.CompletedTask;
    }
}
