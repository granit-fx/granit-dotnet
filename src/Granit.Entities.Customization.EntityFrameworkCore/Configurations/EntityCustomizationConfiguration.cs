using System.Text.Json;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Entities.Customization.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="EntityCustomization"/>.
/// Table: <c>entities_customization_entity_customizations</c> (or
/// <c>{prefix}entity_customizations</c>).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="EntityCustomization.Deltas"/> list is stored as a JSON
/// document via a value converter. The framework keeps the column type
/// portable (default text on PostgreSQL, TEXT on SQLite, nvarchar(max) on
/// SQL Server). Postgres-hosted apps that want JSONB query support
/// (e.g. <c>WHERE deltas @&gt; '[{"fieldName": "X"}]'::jsonb</c>) can
/// <c>ALTER COLUMN deltas TYPE jsonb USING deltas::jsonb</c> in their first
/// migration — no framework migrations ship per CLAUDE.md convention.
/// </para>
/// </remarks>
internal sealed class EntityCustomizationConfiguration : IEntityTypeConfiguration<EntityCustomization>
{
    private static readonly JsonSerializerOptions DeltaJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ValueConverter<IReadOnlyList<LayoutDelta>, string> DeltasConverter =
        new(
            v => JsonSerializer.Serialize(v, DeltaJsonOptions),
            v => Deserialize(v));

    private static readonly ValueComparer<IReadOnlyList<LayoutDelta>> DeltasComparer =
        new(
            (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (h, d) => HashCode.Combine(h, d)),
            v => v.ToList());

    public void Configure(EntityTypeBuilder<EntityCustomization> builder)
    {
        builder.ToTable(
            GranitEntitiesCustomizationDbProperties.DbTablePrefix + "entity_customizations",
            GranitEntitiesCustomizationDbProperties.DbSchema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.LayoutKind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Deltas)
            .HasConversion(DeltasConverter, DeltasComparer)
            .IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        // Manifest composer lookup path — one customization per (tenant,
        // entity, layout-kind). Uniqueness enforced + the composite covers
        // every read against this table.
        builder.HasIndex(x => new { x.TenantId, x.EntityName, x.LayoutKind })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitEntitiesCustomizationDbProperties.DbTablePrefix}entity_customizations_lookup");
    }

    private static List<LayoutDelta> Deserialize(string raw) =>
        string.IsNullOrEmpty(raw)
            ? []
            : JsonSerializer.Deserialize<List<LayoutDelta>>(raw, DeltaJsonOptions) ?? [];
}
