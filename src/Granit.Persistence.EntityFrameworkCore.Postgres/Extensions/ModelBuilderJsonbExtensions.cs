using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;

/// <summary>
/// PostgreSQL-specific <see cref="ModelBuilder"/> conventions for Granit.
/// </summary>
public static class ModelBuilderJsonbExtensions
{
    /// <summary>
    /// Upgrades every property marked by
    /// <c>HasJsonConversion</c> (see
    /// <see cref="JsonPropertyBuilderExtensions"/>) to PostgreSQL <c>jsonb</c>
    /// storage, unless the configuration already pinned an explicit column type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this from <c>OnModelCreating</c> of a Granit-based <c>DbContext</c> that
    /// targets PostgreSQL — typically right after <c>ApplyGranitConventions</c>. The
    /// method walks every property of every entity type and looks for the
    /// <see cref="GranitPersistenceAnnotationNames.JsonSerialized"/> marker. Matching
    /// properties get their column type set to <c>jsonb</c>, which lets Postgres
    /// validate the payload, build GIN indexes on it, and query into the document
    /// via <c>@&gt;</c>, <c>jsonb_path_query</c>, etc.
    /// </para>
    /// <para>
    /// Idempotent: if a property already declares an explicit column type (e.g. via
    /// <see cref="JsonbPropertyBuilderExtensions.HasJsonbConversion{T}"/> or a
    /// hand-written <c>HasColumnType</c>), it is left untouched.
    /// </para>
    /// <para>
    /// Does NOT touch owned types persisted via <c>OwnsOne(...).ToJson()</c> /
    /// <c>OwnsMany(...).ToJson()</c> — Npgsql already maps those to <c>jsonb</c>
    /// by default.
    /// </para>
    /// </remarks>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder UseGranitJsonbForJsonProperties(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.FindAnnotation(GranitPersistenceAnnotationNames.JsonSerialized)?.Value is not true)
                {
                    continue;
                }

                if (property.GetColumnType() is not null)
                {
                    continue;
                }

                property.SetColumnType("jsonb");
            }
        }

        return modelBuilder;
    }
}
