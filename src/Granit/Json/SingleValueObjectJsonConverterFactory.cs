using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Domain;

namespace Granit.Json;

/// <summary>
/// <see cref="JsonConverterFactory"/> that serializes <see cref="SingleValueObject{T}"/>
/// subclasses as their underlying primitive — <c>"text/html"</c> instead of
/// <c>{"value":"text/html"}</c>.
/// </summary>
/// <remarks>
/// Automatically registered for minimal API endpoints by <c>AddGranit()</c>.
/// For other serialization contexts, add manually via
/// <c>JsonSerializerOptions.Converters.Add(new SingleValueObjectJsonConverterFactory())</c>.
/// </remarks>
public sealed class SingleValueObjectJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        GetSingleValueObjectPrimitiveType(typeToConvert) is not null;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type primitiveType = GetSingleValueObjectPrimitiveType(typeToConvert)
            ?? throw new InvalidOperationException(
                $"{typeToConvert.Name} is not a SingleValueObject<T>.");

        Type converterType = typeof(SingleValueObjectJsonConverter<,>)
            .MakeGenericType(typeToConvert, primitiveType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static Type? GetSingleValueObjectPrimitiveType(Type type)
    {
        Type? current = type;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(SingleValueObject<>))
            {
                return current.GetGenericArguments()[0];
            }

            current = current.BaseType;
        }

        return null;
    }

    [SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via Activator.CreateInstance in CreateConverter.")]
    private sealed class SingleValueObjectJsonConverter<TValueObject, TPrimitive> : JsonConverter<TValueObject>
        where TValueObject : SingleValueObject<TPrimitive>
        where TPrimitive : notnull
    {
        public override TValueObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            TPrimitive? primitive = JsonSerializer.Deserialize<TPrimitive>(ref reader, options);
            if (primitive is null)
            {
                return null;
            }

            // Use the runtime's compiled expression or reflection to create the instance.
            // SingleValueObject<T> requires a public init property 'Value', so we create
            // using the parameterless constructor (EF Core compat) and set Value.
            var instance = (TValueObject)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeToConvert);

            // Find and set the Value property via reflection (once per type, acceptable for deserialization).
            System.Reflection.PropertyInfo valueProperty = typeToConvert.GetProperty(nameof(SingleValueObject<TPrimitive>.Value))
                ?? throw new JsonException($"Type {typeToConvert.Name} does not have a Value property.");

            valueProperty.SetValue(instance, primitive);

            return instance;
        }

        public override void Write(Utf8JsonWriter writer, TValueObject value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Value, options);
    }
}
