using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers.Compatibility;

/// <summary>
/// Emits a concrete schema for <see cref="JsonElement"/> (and related System.Text.Json
/// dynamic JSON types) so code generators such as orval don't encounter a <c>$ref</c> to
/// an undeclared component schema. Without this transformer, ASP.NET Core OpenAPI emits
/// an empty object schema for these types, which orval rejects as "unknown type".
/// </summary>
/// <remarks>
/// The replacement schema declares a JSON 3.1 union of <c>object</c>, <c>array</c>,
/// <c>string</c>, <c>number</c>, <c>boolean</c> and <c>null</c> — the exact set of
/// values a <see cref="JsonElement"/> can represent at runtime.
/// </remarks>
internal sealed class JsonElementSchemaTransformer : IOpenApiSchemaTransformer
{
    private const JsonSchemaType AnyJsonValue =
        JsonSchemaType.Object
        | JsonSchemaType.Array
        | JsonSchemaType.String
        | JsonSchemaType.Number
        | JsonSchemaType.Boolean
        | JsonSchemaType.Null;

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type underlying = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
            ?? context.JsonTypeInfo.Type;

        if (underlying == typeof(JsonElement)
            || underlying == typeof(JsonDocument)
            || underlying == typeof(JsonNode)
            || underlying == typeof(JsonValue))
        {
            schema.Type = AnyJsonValue;
            schema.Properties = null;
            schema.AdditionalProperties = null;
            schema.Required = null;
            schema.Description ??= "Arbitrary JSON value (object, array, string, number, boolean, or null).";
        }

        return Task.CompletedTask;
    }
}
