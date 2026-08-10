using System.Globalization;
using System.Text.Json.Nodes;
using Granit.Validation.JsonSchema;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Validation.OpenApi;

/// <summary>
/// OpenAPI schema transformer that enriches schemas with validation constraints
/// extracted from registered <see cref="FluentValidation.IValidator{T}"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Delegates constraint extraction to <see cref="IJsonSchemaWriter"/> and projects
/// the resulting JSON Schema fragment onto the in-place <see cref="OpenApiSchema"/>.
/// Behavior is identical to the prior in-house implementation: <c>maxLength</c>,
/// <c>minLength</c>, <c>pattern</c>, <c>minimum</c>, <c>maximum</c>, <c>required</c>,
/// <c>format</c>, plus the <c>x-granit-validator</c> and <c>x-granit-pattern-hint</c>
/// extensions.
/// </para>
/// </remarks>
internal sealed class FluentValidationSchemaTransformer(IJsonSchemaWriter writer) : IOpenApiSchemaTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return Task.CompletedTask;
        }

        JsonObject? jsonSchema = writer.Write(context.JsonTypeInfo.Type);
        if (jsonSchema is null)
        {
            return Task.CompletedTask;
        }

        Project(schema, jsonSchema);
        return Task.CompletedTask;
    }

    private static void Project(OpenApiSchema schema, JsonObject jsonSchema)
    {
        if (jsonSchema["required"] is JsonArray requiredArray)
        {
            foreach (JsonNode? node in requiredArray)
            {
                if (node?.GetValue<string>() is { } name)
                {
                    schema.Required ??= new HashSet<string>();
                    schema.Required.Add(name);
                }
            }
        }

        if (jsonSchema["properties"] is not JsonObject jsonProperties)
        {
            return;
        }

        foreach (KeyValuePair<string, IOpenApiSchema> property in schema.Properties!)
        {
            if (property.Value is not OpenApiSchema propertySchema)
            {
                continue;
            }

            if (jsonProperties[property.Key] is not JsonObject constraints)
            {
                continue;
            }

            ProjectProperty(propertySchema, constraints);
        }
    }

    private static void ProjectProperty(OpenApiSchema propertySchema, JsonObject constraints)
    {
        if (constraints["maxLength"]?.GetValue<int>() is { } maxLength)
        {
            propertySchema.MaxLength = maxLength;
        }

        if (constraints["minLength"]?.GetValue<int>() is { } minLength)
        {
            propertySchema.MinLength = minLength;
        }

        if (constraints["pattern"]?.GetValue<string>() is { } pattern)
        {
            propertySchema.Pattern = pattern;
        }

        if (constraints["format"]?.GetValue<string>() is { } format)
        {
            propertySchema.Format = format;
        }

        if (FormatNumeric(constraints["minimum"]) is { } minimum)
        {
            propertySchema.Minimum = minimum;
        }

        if (FormatNumeric(constraints["maximum"]) is { } maximum)
        {
            propertySchema.Maximum = maximum;
        }

        if (FormatNumeric(constraints["exclusiveMinimum"]) is { } exclusiveMin)
        {
            propertySchema.ExclusiveMinimum = exclusiveMin;
        }

        if (FormatNumeric(constraints["exclusiveMaximum"]) is { } exclusiveMax)
        {
            propertySchema.ExclusiveMaximum = exclusiveMax;
        }

        if (constraints["x-granit-validator"]?.GetValue<string>() is { } validatorCode)
        {
            propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            propertySchema.Extensions["x-granit-validator"] =
                new JsonNodeExtension(JsonValue.Create(validatorCode));
        }

        if (constraints["x-granit-pattern-hint"]?.GetValue<string>() is { } hintKey)
        {
            propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            propertySchema.Extensions["x-granit-pattern-hint"] =
                new JsonNodeExtension(JsonValue.Create(hintKey));
        }
    }

    private static string? FormatNumeric(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        // Match the prior behavior: numeric bounds are surfaced as
        // invariant-culture strings on OpenApiSchema.
        if (value.TryGetValue(out int intValue))
        {
            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue(out long longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue(out decimal decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue(out double doubleValue))
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue(out string? stringValue))
        {
            return stringValue;
        }

        return null;
    }
}
