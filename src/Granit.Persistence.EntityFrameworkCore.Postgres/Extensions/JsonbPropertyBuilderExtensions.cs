using System.Text.Json;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;

/// <summary>
/// PostgreSQL-specific extensions for configuring JSON-serialized properties.
/// </summary>
public static class JsonbPropertyBuilderExtensions
{
    /// <summary>
    /// Configures a property to be persisted as PostgreSQL <c>jsonb</c>, with the same
    /// JSON serialization and deep-equality semantics as
    /// <see cref="JsonPropertyBuilderExtensions.HasJsonConversion{T}(PropertyBuilder{T}, JsonSerializerOptions?)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Storing as <c>jsonb</c> (rather than <c>text</c>) lets PostgreSQL validate the payload,
    /// build GIN indexes on it, and query into the document via <c>@&gt;</c>, <c>jsonb_path_query</c>,
    /// etc. The serialization is still string-based on the CLR side — this helper does not
    /// enable EF Core's owned-type-to-JSON translation, only the column storage type.
    /// </para>
    /// <para>
    /// Use this helper only when the consuming application targets PostgreSQL. Calling it
    /// against a SQL Server provider will produce a migration referencing the <c>jsonb</c>
    /// type, which SQL Server does not understand.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The property CLR type.</typeparam>
    /// <param name="builder">The property builder to configure.</param>
    /// <param name="options">Optional <see cref="JsonSerializerOptions"/>; defaults to STJ defaults.</param>
    /// <returns>The same <see cref="PropertyBuilder{TProperty}"/> for chaining.</returns>
    public static PropertyBuilder<T> HasJsonbConversion<T>(
        this PropertyBuilder<T> builder,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.HasJsonConversion(options).HasColumnType("jsonb");
    }
}
