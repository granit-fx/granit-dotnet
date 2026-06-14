using Granit.AI.Prompts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Prompts.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="PromptCategory"/> aggregate root.</summary>
internal sealed class PromptCategoryConfiguration : IEntityTypeConfiguration<PromptCategory>
{
    public void Configure(EntityTypeBuilder<PromptCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIPromptsDbProperties.DbTablePrefix + "categories",
            GranitAIPromptsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.ModifiedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Category names are unique within a tenant.
        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitAIPromptsDbProperties.DbTablePrefix}categories_tenant_name");
    }
}
