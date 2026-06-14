using Granit.AI.Prompts.Domain;
using Granit.QueryEngine;

namespace Granit.AI.Prompts.Queries;

/// <summary>
/// Query definition for prompt categories — declares the admin-grid columns, filters, sorting, and
/// search for the query engine (ADR-020 pairing with <see cref="Exports.PromptCategoryExportDefinition"/>).
/// </summary>
public sealed class PromptCategoryQueryDefinition : QueryDefinition<PromptCategory>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.Prompts.CategoriesQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(AIPromptsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PromptCategory> builder)
    {
        builder
            .Column(c => c.Name, col => col.Label("Name").LabelKey("AIPrompts.Columns.Name").Filterable().Sortable())
            .Column(c => c.IsSystem, col => col.Label("System").LabelKey("AIPrompts.Columns.IsSystem").Filterable().Sortable())
            .Column(c => c.TenantId, col => col.Label("Tenant").LabelKey("AIPrompts.Columns.Tenant").Filterable())
            .Column(c => c.CreatedAt, col => col.Label("Created At").LabelKey("AIPrompts.Columns.CreatedAt").Sortable())
            .GlobalSearch(c => c.Name)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
