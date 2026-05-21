using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Extensions for configuring JSON-serialized properties on EF Core entities.
/// </summary>
public static class JsonPropertyBuilderExtensions
{
    /// <summary>
    /// Configures a property to be persisted as a JSON-serialized string, with a deep-equality
    /// <see cref="ValueComparer{T}"/> wired so EF Core change detection works on mutable payloads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provider-agnostic: the column type is left to the database provider
    /// (<c>nvarchar(max)</c> on SQL Server, <c>text</c> on Npgsql). Apps targeting PostgreSQL that
    /// want a queryable <c>jsonb</c> column can chain <c>.HasColumnType("jsonb")</c> in their own
    /// configuration, or rely on a provider-specific helper from
    /// <c>Granit.Persistence.EntityFrameworkCore.Postgres</c>.
    /// </para>
    /// <para>
    /// Equality and hash are computed by re-serializing both operands — correct for any
    /// <see cref="JsonSerializer"/>-serializable type (collections, POCOs, <see cref="JsonElement"/>),
    /// at the cost of one serialization per change-tracking check. For very hot paths consider a
    /// hand-rolled comparer instead.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The property CLR type.</typeparam>
    /// <param name="builder">The property builder to configure.</param>
    /// <param name="options">
    /// Optional <see cref="JsonSerializerOptions"/>. When <see langword="null"/> the STJ defaults
    /// are used — matches the existing per-site pattern in the framework.
    /// </param>
    /// <returns>The same <see cref="PropertyBuilder{TProperty}"/> for chaining.</returns>
    public static PropertyBuilder<T> HasJsonConversion<T>(
        this PropertyBuilder<T> builder,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ValueConverter<T, string> converter = new(
            v => JsonSerializer.Serialize(v, options),
            v => JsonSerializer.Deserialize<T>(v, options)!);

        // Lambdas are converted to Expression<> trees by EF's ValueComparer ctor, so any C#
        // construct disallowed in expression trees (pattern matching, switch expressions, etc.)
        // cannot appear here. JsonSerializer handles null inputs by emitting the "null" literal,
        // so explicit null checks are unnecessary.
        ValueComparer<T> comparer = new(
            (a, b) => JsonSerializer.Serialize(a, options) == JsonSerializer.Serialize(b, options),
            v => JsonSerializer.Serialize(v, options).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, options), options)!);

        builder.HasConversion(converter, comparer);
        builder.Metadata.SetAnnotation(GranitPersistenceAnnotationNames.JsonSerialized, true);
        return builder;
    }
}
