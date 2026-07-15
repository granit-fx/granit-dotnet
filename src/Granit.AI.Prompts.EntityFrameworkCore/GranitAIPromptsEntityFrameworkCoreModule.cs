using Granit.AI.Prompts.EntityFrameworkCore.Seeding;
using Granit.Modularity;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Prompts.EntityFrameworkCore;

/// <summary>
/// Granit module registering EF Core persistence for the prompt catalogue. The host configures the
/// <see cref="Internal.AIPromptsDbContext"/> (connection string); this module registers the
/// <see cref="IPromptTemplateStore"/> and the per-tenant generic-prompt data seeder.
/// </summary>
[DependsOn(
    typeof(GranitAIPromptsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAIPromptsEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddTransient<ITenantDataSeedContributor, PromptDataSeedContributor>();
}
