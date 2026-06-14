using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore;

/// <summary>
/// Granit module registering EF Core persistence for the prompt catalogue. The host configures the
/// <see cref="Internal.AIPromptsDbContext"/> (connection string); this module registers the
/// <see cref="IPromptTemplateStore"/>.
/// </summary>
[DependsOn(
    typeof(GranitAIPromptsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAIPromptsEntityFrameworkCoreModule : GranitModule;
