using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Persistence.EntityFrameworkCore.ValueConverters;

/// <summary>
/// Named EF Core <see cref="ValueComparer{T}"/> for JSON-serialized multi-field value
/// objects. Deep-equality is computed by re-serializing both operands — correct for any
/// STJ-serializable type, at the cost of one serialization per change-tracking check.
/// </summary>
/// <remarks>
/// Using a named, parameterless-constructor class instead of inline lambdas lets EF Core
/// reproduce the comparer in migration snapshot code alongside
/// <see cref="JsonValueObjectConverter{T}"/>, ensuring full round-trip stability.
/// </remarks>
/// <typeparam name="T">The multi-field value object CLR type.</typeparam>
public sealed class JsonValueObjectComparer<T>()
    : ValueComparer<T>(
        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
