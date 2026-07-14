using Granit.Domain;
using Granit.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Rewrites schemas of <see cref="SingleValueObject{T}"/> subclasses to the schema of
/// the underlying primitive <c>T</c>, matching the wire format produced by
/// <see cref="SingleValueObjectJsonConverterFactory"/> (which serializes
/// <c>ContentType</c> as <c>"text/html"</c>, not <c>{"value":"text/html"}</c>).
/// Without this transformer the generated document describes an object with a
/// <c>value</c> property that never appears on the wire.
/// </summary>
internal sealed class SingleValueObjectSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
            ?? context.JsonTypeInfo.Type;

        // Single source of truth for "is this a SingleValueObject<T> and what is T" —
        // shared with the JSON converter factory so schema and wire format cannot drift.
        Type? primitiveType =
            SingleValueObjectJsonConverterFactory.GetSingleValueObjectPrimitiveType(type);

        if (primitiveType is null)
        {
            return Task.CompletedTask;
        }

        (JsonSchemaType schemaType, string? format) = MapPrimitive(primitiveType);

        schema.Type = schemaType;
        schema.Format = format;
        schema.Properties = null;
        schema.Required = null;
        schema.AdditionalProperties = null;
        schema.AdditionalPropertiesAllowed = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps the CLR primitive to its OpenAPI 3.1 type/format pair, mirroring how
    /// System.Text.Json serializes the value (which is what
    /// <c>SingleValueObjectJsonConverterFactory</c> delegates to).
    /// </summary>
    private static (JsonSchemaType Type, string? Format) MapPrimitive(Type primitiveType) =>
        primitiveType switch
        {
            _ when primitiveType == typeof(string) => (JsonSchemaType.String, null),
            _ when primitiveType == typeof(Guid) => (JsonSchemaType.String, "uuid"),
            _ when primitiveType == typeof(Uri) => (JsonSchemaType.String, "uri"),
            _ when primitiveType == typeof(DateTimeOffset) => (JsonSchemaType.String, "date-time"),
            _ when primitiveType == typeof(DateTime) => (JsonSchemaType.String, "date-time"),
            _ when primitiveType == typeof(DateOnly) => (JsonSchemaType.String, "date"),
            _ when primitiveType == typeof(TimeOnly) => (JsonSchemaType.String, "time"),
            _ when primitiveType == typeof(bool) => (JsonSchemaType.Boolean, null),
            _ when primitiveType == typeof(int) => (JsonSchemaType.Integer, "int32"),
            _ when primitiveType == typeof(short) => (JsonSchemaType.Integer, "int32"),
            _ when primitiveType == typeof(byte) => (JsonSchemaType.Integer, "int32"),
            _ when primitiveType == typeof(long) => (JsonSchemaType.Integer, "int64"),
            _ when primitiveType == typeof(float) => (JsonSchemaType.Number, "float"),
            _ when primitiveType == typeof(double) => (JsonSchemaType.Number, "double"),
            _ when primitiveType == typeof(decimal) => (JsonSchemaType.Number, "double"),
            // Unknown primitive (e.g. a nested value object): keep it schematically open
            // rather than emitting a misleading object shape.
            _ => (JsonSchemaType.String, null),
        };
}
