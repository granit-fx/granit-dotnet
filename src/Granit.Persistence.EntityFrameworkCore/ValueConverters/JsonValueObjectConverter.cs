using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Persistence.EntityFrameworkCore.ValueConverters;

/// <summary>
/// Named EF Core <see cref="ValueConverter{TModel,TProvider}"/> that serializes a
/// multi-field value object as a JSON string column.
/// </summary>
/// <remarks>
/// Using a named, parameterless-constructor class instead of inline lambdas lets EF Core
/// reproduce the converter in migration snapshot code
/// (<c>new JsonValueObjectConverter&lt;T&gt;()</c>), preventing perpetual
/// <c>PendingModelChangesWarning</c> when the snapshot CLR type and the runtime converter
/// are compared. Mirrors the pattern of <see cref="SingleValueObjectConverter{TValueObject,TPrimitive}"/>.
/// </remarks>
/// <typeparam name="T">The multi-field value object CLR type.</typeparam>
public sealed class JsonValueObjectConverter<T>()
    : ValueConverter<T, string>(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null)!);
