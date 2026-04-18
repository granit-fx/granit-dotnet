using Granit.QueryEngine;
using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Queries;

/// <summary>
/// Query definition for dynamic reference data entries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class DynamicReferenceDataEntityQueryDefinition : QueryDefinition<DynamicReferenceDataEntity>
{
    /// <inheritdoc/>
    public override string Name => "Granit.ReferenceData.DynamicReferenceDataEntityQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<DynamicReferenceDataEntity> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("ReferenceData.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.Code, c => c.Label("Code").LabelKey("ReferenceData.Columns.Code").Filterable().Sortable())
            .Column(e => e.LabelEn, c => c.Label("Label (EN)").LabelKey("ReferenceData.Columns.LabelEn").Filterable().Sortable())
            .Column(e => e.Activated, c => c.Label("Activated").LabelKey("ReferenceData.Columns.Activated").Filterable().Sortable())
            .Column(e => e.SortOrder, c => c.Label("Sort Order").LabelKey("ReferenceData.Columns.SortOrder").Sortable())
            .Column(e => e.ParentCode, c => c.Label("Parent Code").LabelKey("ReferenceData.Columns.ParentCode").Filterable().Sortable())
            .Column(e => e.ValidFrom, c => c.Label("Valid From").LabelKey("ReferenceData.Columns.ValidFrom").Sortable())
            .Column(e => e.ValidTo, c => c.Label("Valid To").LabelKey("ReferenceData.Columns.ValidTo").Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("ReferenceData.Columns.CreatedAt").Sortable())
            .Column(e => e.ModifiedAt, c => c.Label("Modified At").LabelKey("ReferenceData.Columns.ModifiedAt").Sortable())
            .GlobalSearch(e => e.Code, e => e.LabelEn, e => e.ParentCode)
            .DateFilter(e => e.ValidFrom)
            .DefaultSort("sortOrder")
            .DefaultPageSize(25);
    }
}
