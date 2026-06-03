using System.Text.Json;
using System.Text.Json.Serialization;

namespace Granit.Json;

/// <summary>
/// The single source of truth for "how Granit serializes JSON". Adds the canonical Granit converters
/// to a <see cref="JsonSerializerOptions"/>: a <see cref="JsonStringEnumConverter"/> (enums as their
/// names) and a <see cref="SingleValueObjectJsonConverterFactory"/> (flattens
/// <see cref="Granit.Domain.SingleValueObject{T}"/> to its underlying primitive). Applied to both the
/// minimal-API pipeline and the cache L2 serializer so a value object round-trips identically wherever
/// it is serialized.
/// </summary>
public static class GranitJsonConverters
{
    /// <summary>
    /// Adds the canonical Granit converters to <paramref name="options"/> and returns it. Idempotent:
    /// a converter of the same type already present is left in place, so it is safe to call on
    /// host-supplied options or more than once.
    /// </summary>
    public static JsonSerializerOptions AddGranitJsonConverters(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Converters.Any(c => c is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        if (!options.Converters.Any(c => c is SingleValueObjectJsonConverterFactory))
        {
            options.Converters.Add(new SingleValueObjectJsonConverterFactory());
        }

        return options;
    }
}
