using Granit.AI.Prompts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Prompts.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="PromptTemplate"/> aggregate root.</summary>
internal sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIPromptsDbProperties.DbTablePrefix + "templates",
            GranitAIPromptsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ShortDescription).HasMaxLength(500);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.Icon).HasMaxLength(100);
        // Hex colour with leading '#': #RRGGBB (7) or #RRGGBBAA (9, with alpha).
        builder.Property(e => e.IconColor).HasMaxLength(9);
        builder.Property(e => e.OwnerId).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.ModifiedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Catalogue listing: system prompts + the owner's, within the tenant, ordered by name.
        builder.HasIndex(e => new { e.TenantId, e.IsSystem, e.OwnerId, e.Name })
            .HasDatabaseName($"ix_{GranitAIPromptsDbProperties.DbTablePrefix}templates_tenant_system_owner_name");

        // A link belongs to exactly one prompt; deleting the prompt cascades to its links.
        builder.HasMany(e => e.CategoryLinks)
            .WithOne()
            .HasForeignKey(l => l.PromptTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
