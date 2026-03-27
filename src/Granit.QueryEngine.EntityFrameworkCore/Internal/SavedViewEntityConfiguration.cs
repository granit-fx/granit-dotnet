using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core configuration for <see cref="SavedView"/>.
/// </summary>
internal sealed class SavedViewEntityConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable(
            GranitQueryEngineDbProperties.DbTablePrefix + "saved_views",
            GranitQueryEngineDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsShared).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired();
        builder.Property(e => e.FilterJson).HasMaxLength(10_000);
        builder.Property(e => e.SortJson).HasMaxLength(10_000);
        builder.Property(e => e.GroupByJson).HasMaxLength(10_000);
        builder.Property(e => e.VisibleColumnsJson).HasMaxLength(10_000);
        builder.Property(e => e.TenantId);

        // Audit trail (ISO 27001)
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ModifiedAt);
        builder.Property(e => e.ModifiedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.EntityType, e.UserId, e.Name, e.TenantId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitQueryEngineDbProperties.DbTablePrefix}saved_views_entity_user_name_tenant");
    }
}
