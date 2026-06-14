using Granit.AI.Prompts.Domain;
using Granit.QueryEngine;

namespace Granit.AI.Prompts.Queries;

/// <summary>
/// Query definition for the prompt catalogue — declares the admin-grid columns, filters, sorting, and
/// search for the query engine (ADR-020 pairing with <see cref="Exports.PromptTemplateExportDefinition"/>).
/// </summary>
public sealed class PromptTemplateQueryDefinition : QueryDefinition<PromptTemplate>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.Prompts.TemplatesQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(AIPromptsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<PromptTemplate> builder)
    {
        builder
            .Column(p => p.Name, c => c.Label("Name").LabelKey("AIPrompts.Columns.Name").Filterable().Sortable())
            .Column(p => p.ShortDescription, c => c.Label("Description").LabelKey("AIPrompts.Columns.Description").Filterable())
            .Column(p => p.IsSystem, c => c.Label("System").LabelKey("AIPrompts.Columns.IsSystem").Filterable().Sortable())
            .Column(p => p.OwnerId, c => c.Label("Owner").LabelKey("AIPrompts.Columns.Owner").Filterable())
            .Column(p => p.Version, c => c.Label("Version").LabelKey("AIPrompts.Columns.Version").Sortable())
            .Column(p => p.TenantId, c => c.Label("Tenant").LabelKey("AIPrompts.Columns.Tenant").Filterable())
            .Column(p => p.CreatedAt, c => c.Label("Created At").LabelKey("AIPrompts.Columns.CreatedAt").Sortable())
            .GlobalSearch(p => p.Name)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
