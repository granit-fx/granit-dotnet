using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers.Compatibility;

/// <summary>
/// Adds placeholder examples to OpenAPI responses whose schema is a pure dictionary
/// (<c>type: object</c> + <c>additionalProperties</c> without named <c>properties</c>).
/// Without examples, Scalar displays "Unknown Property Name" for dictionary key placeholders.
/// </summary>
internal sealed class DictionarySchemaExampleOperationTransformer : IOpenApiOperationTransformer
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

        foreach ((string _, IOpenApiResponse value) in operation.Responses)
        {
            if (value is not OpenApiResponse response || response.Content is null)
            {
                continue;
            }

            foreach ((string _, OpenApiMediaType mediaType) in response.Content)
            {
                AddDictionaryExample(mediaType.Schema);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// If the schema is a pure dictionary (object + additionalProperties, no named properties),
    /// adds a two-entry example so Scalar can render meaningful placeholder keys.
    /// </summary>
    private static void AddDictionaryExample(IOpenApiSchema? schema)
    {
        if (schema is not OpenApiSchema s)
        {
            return;
        }

        // Skip schemas that already have an example or have named properties.
        if (s.Example is not null || s.Examples?.Count > 0)
        {
            return;
        }

        if (s.AdditionalProperties is null || (s.Properties?.Count ?? 0) > 0)
        {
            return;
        }

        bool isObject = s.Type == JsonSchemaType.Object
            || (s.Type is null && s.AdditionalProperties is not null);

        if (!isObject)
        {
            return;
        }

        s.Example = BuildExample(s.AdditionalProperties!);
    }

    private static JsonObject? BuildExample(IOpenApiSchema additionalProperties)
    {
        if (additionalProperties is OpenApiSchema inner)
        {
            JsonNode? sampleValue = inner.Type switch
            {
                JsonSchemaType.String => JsonValue.Create("value"),
                JsonSchemaType.Integer => JsonValue.Create(0),
                JsonSchemaType.Number => JsonValue.Create(0.0),
                JsonSchemaType.Boolean => JsonValue.Create(true),
                _ => JsonValue.Create("value"),
            };

            return new JsonObject
            {
                ["key1"] = sampleValue?.DeepClone(),
                ["key2"] = sampleValue?.DeepClone(),
            };
        }

        return null;
    }
}
