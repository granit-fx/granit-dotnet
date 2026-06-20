using Granit.QueryEngine;
using Granit.Templating.Store;

namespace Granit.Templating.Queries;

/// <summary>
/// Query definition for template summaries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class TemplateSummaryQueryDefinition : QueryDefinition<TemplateSummary>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Templating.TemplateSummaryQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(TemplateLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TemplateSummary> builder)
    {
        builder
            .Column(s => s.Name, c => c.Label("Name").LabelKey("Template.Columns.Name").Filterable().Sortable())
            .Column(s => s.Culture, c => c.Label("Culture").LabelKey("Template.Columns.Culture").Filterable().Sortable())
            .Column(s => s.MimeType, c => c.Label("MIME Type").LabelKey("Template.Columns.MimeType").Filterable())
            .Column(s => s.CurrentStatus, c => c.Label("Status").LabelKey("Template.Columns.Status").Filterable().Sortable())
            .Column(s => s.HasPublishedVersion, c => c.Label("Has Published Version").LabelKey("Template.Columns.HasPublishedVersion").Filterable())
            .Column(s => s.LayoutName, c => c.Label("Layout").LabelKey("Template.Columns.LayoutName").Filterable())
            .Column(s => s.CategoryId, c => c.Label("Category").LabelKey("Template.Columns.Category").Filterable().Lookup("template-categories", requiredPermission: "Templating.Categories.Read"))
            .Column(s => s.LastModifiedAt, c => c.Label("Last Modified").LabelKey("Template.Columns.LastModifiedAt").Sortable())
            .Column(s => s.LastModifiedBy, c => c.Label("Last Modified By").LabelKey("Template.Columns.LastModifiedBy").Filterable())
            .GlobalSearch(s => s.Name)
            .DateFilter(s => s.LastModifiedAt)
            .AllowGroupBy(s => s.CurrentStatus)
            .AllowGroupBy(s => s.CategoryId)
            .DefaultSort("name")
            .DefaultPageSize(25);
    }
}
