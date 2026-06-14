using Granit.AI.Prompts.EntityFrameworkCore.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Extensions;

/// <summary>
/// Applies the AI Prompts entity configurations. The host owns migrations and can call this from a
/// shared DbContext if it does not use the isolated one.
/// </summary>
public static class AIPromptsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit AI Prompts module.</summary>
    public static ModelBuilder ConfigureAIPromptsModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new PromptTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTemplateCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new PromptCategoryConfiguration());
        return modelBuilder;
    }
}
