using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Normalizes <c>int</c> properties on response/request schemas. ASP.NET Core's OpenAPI
/// generator emits them as <c>type: ["integer", "string"]</c> with a numeric pattern
/// because System.Text.Json accepts both representations on read. The string fallback is
/// only meaningful for <c>long</c> (JavaScript's <c>Number</c> precision tops out at 2^53);
/// for <c>int32</c> it just adds noise and confuses code generators.
/// </summary>
internal sealed class Int32SchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Normalize(schema);

        if (schema.Properties is { } props)
        {
            foreach (IOpenApiSchema property in props.Values)
            {
                if (property is OpenApiSchema concrete)
                {
                    Normalize(concrete);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void Normalize(OpenApiSchema schema)
    {
        if (schema.Format != "int32")
        {
            return;
        }

        if (schema.Type == (JsonSchemaType.Integer | JsonSchemaType.String))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Pattern = null;
        }
        else if (schema.Type == (JsonSchemaType.Null | JsonSchemaType.Integer | JsonSchemaType.String))
        {
            schema.Type = JsonSchemaType.Null | JsonSchemaType.Integer;
            schema.Pattern = null;
        }
    }
}
