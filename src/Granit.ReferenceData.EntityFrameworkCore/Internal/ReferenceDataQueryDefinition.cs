using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Options;

namespace Granit.ReferenceData.EntityFrameworkCore.Internal;

/// <summary>
/// Auto-generated <see cref="QueryDefinition{TEntity}"/> for a dynamically registered
/// reference data type. Declares base columns (Code, Labels, IsActive, SortOrder, ValidFrom,
/// ValidTo) plus any Shadow Property columns from <see cref="ReferenceDataExtensionOptions"/>.
/// </summary>
internal sealed class ReferenceDataQueryDefinition(
    string typeName,
    ReferenceDataExtensionOptions extensionOptions)
    : QueryDefinition<DynamicReferenceDataEntity>
{
    /// <inheritdoc/>
    public override string Name => $"ReferenceData.{typeName}";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<DynamicReferenceDataEntity> builder)
    {
        // Base columns (always present on ReferenceDataEntity)
        builder
            .Column(e => e.Code, c => c.Label("Code").Filterable().Sortable())
            .Column(e => e.LabelEn, c => c.Label("Label (EN)").Filterable().Sortable())
            .Column(e => e.LabelFr, c => c.Label("Label (FR)").Filterable())
            .Column(e => e.LabelNl, c => c.Label("Label (NL)").Filterable())
            .Column(e => e.LabelDe, c => c.Label("Label (DE)").Filterable())
            .Column(e => e.IsActive, c => c.Label("Active").Filterable())
            .Column(e => e.SortOrder, c => c.Label("Sort Order").Sortable())
            .Column(e => e.ValidFrom, c => c.Label("Valid From").Filterable().Sortable())
            .Column(e => e.ValidTo, c => c.Label("Valid To").Filterable().Sortable());

        if (extensionOptions.IsHierarchical)
        {
            builder.Column(e => e.ParentCode, c => c.Label("Parent Code").Filterable());
        }

        // Dynamic mapped properties (Shadow Properties)
        foreach (ReferenceDataPropertyMapping mapping in extensionOptions.PropertyMappings)
        {
            builder.ShadowColumn(mapping.Name, mapping.ClrType, c =>
            {
                c.Label(mapping.Name);
                if (mapping.IsFilterable)
                {
                    c.Filterable();
                }

                if (mapping.IsSortable)
                {
                    c.Sortable();
                }
            });
        }

        // Global search on Code + EN/FR labels
        builder.GlobalSearch(e => e.Code, e => e.LabelEn, e => e.LabelFr);

        // Default sort by SortOrder ascending, then Code
        builder.DefaultSort("SortOrder,Code");
    }
}
