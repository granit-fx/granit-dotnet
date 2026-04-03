using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class PlanExternalMappingConfiguration : IEntityTypeConfiguration<PlanExternalMapping>
{
    public void Configure(EntityTypeBuilder<PlanExternalMapping> builder)
    {
        builder.ToTable(
            GranitSubscriptionsDbProperties.DbTablePrefix + "plan_external_mappings",
            GranitSubscriptionsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(256).IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.ExternalId })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitSubscriptionsDbProperties.DbTablePrefix}plan_ext_provider_id");
    }
}
