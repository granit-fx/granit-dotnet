using Granit.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="Tag"/>.
/// Table: <c>taxonomy_tags</c>.
/// </summary>
internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitTaxonomyDbProperties.DbTablePrefix + "tags",
            GranitTaxonomyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.Scope)
            .HasMaxLength(Tag.MaxScopeLength)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(Tag.MaxNameLength)
            .IsRequired();

        builder.Property(e => e.Color)
            .HasMaxLength(Tag.ColorLength)
            .IsRequired()
            .IsFixedLength();

        builder.Property(e => e.HideOnEntityCard)
            .IsRequired();

        // Optimistic-concurrency token. uint maps to provider-native types
        // (PostgreSQL bigint via npgsql, SQL Server int, SQLite INTEGER); IsConcurrencyToken
        // makes EF compare it on UPDATE so concurrent writers surface as
        // DbUpdateConcurrencyException.
        builder.Property(e => e.RowVersion)
            .IsRequired()
            .IsConcurrencyToken();

        // Tenant + scope + name uniqueness — the load-bearing invariant of ADR-054.
        // Same name allowed across scopes; "VIP" in parties and "VIP" in documents coexist.
        builder.HasIndex(e => new { e.TenantId, e.Scope, e.Name })
            .IsUnique()
            .HasDatabaseName($"ux_{GranitTaxonomyDbProperties.DbTablePrefix}tags_tenant_scope_name");

        // Lookup index for per-scope autocomplete queries (T2.1 lists tags by scope).
        builder.HasIndex(e => new { e.TenantId, e.Scope })
            .HasDatabaseName($"ix_{GranitTaxonomyDbProperties.DbTablePrefix}tags_tenant_scope");
    }
}
