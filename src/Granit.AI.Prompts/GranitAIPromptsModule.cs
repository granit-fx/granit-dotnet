using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.Exports;
using Granit.AI.Prompts.Queries;
using Granit.DataExchange.Extensions;
using Granit.Localization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;

namespace Granit.AI.Prompts;

/// <summary>
/// Granit module for the user-facing prompt catalogue (ADR-067): the
/// <see cref="Domain.PromptTemplate"/> aggregate and the <see cref="IPromptTemplateStore"/>
/// abstraction. Persistence is provided by <c>Granit.AI.Prompts.EntityFrameworkCore</c> and the
/// HTTP surface by <c>Granit.AI.Prompts.Endpoints</c>.
/// </summary>
[DependsOn(typeof(GranitLocalizationModule))]
public sealed class GranitAIPromptsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<AIPromptsLocalizationResource>();

        // Admin-grid + take-out for the catalogue (ADR-020 Query↔Export pairing). The EF queryable
        // source is registered by Granit.AI.Prompts.EntityFrameworkCore.
        context.Services.AddQueryDefinition<PromptTemplate, PromptTemplateQueryDefinition>();
        context.Services.AddExportDefinition<PromptTemplate, PromptTemplateExportDefinition>();
        context.Services.AddQueryDefinition<PromptCategory, PromptCategoryQueryDefinition>();
        context.Services.AddExportDefinition<PromptCategory, PromptCategoryExportDefinition>();
    }
}
