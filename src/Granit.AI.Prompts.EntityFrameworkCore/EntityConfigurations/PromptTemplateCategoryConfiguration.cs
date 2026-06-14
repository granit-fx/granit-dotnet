using Granit.AI.Prompts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Prompts.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="PromptTemplateCategory"/> link entity.</summary>
internal sealed class PromptTemplateCategoryConfiguration : IEntityTypeConfiguration<PromptTemplateCategory>
{
    public void Configure(EntityTypeBuilder<PromptTemplateCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIPromptsDbProperties.DbTablePrefix + "template_categories",
            GranitAIPromptsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // Ids are client-generated (IGuidGenerator); without this EF treats a pre-set key as an
        // existing row and tracks a link added to an existing prompt as Modified instead of Added.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CreatedBy).HasMaxLength(256);

        // A prompt is linked to a given category at most once.
        builder.HasIndex(e => new { e.PromptTemplateId, e.CategoryId })
            .IsUnique()
            .HasDatabaseName($"ix_{GranitAIPromptsDbProperties.DbTablePrefix}template_categories_prompt_category");

        builder.HasIndex(e => e.CategoryId)
            .HasDatabaseName($"ix_{GranitAIPromptsDbProperties.DbTablePrefix}template_categories_category");
    }
}
