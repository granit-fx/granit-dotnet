using Granit.QueryEngine;
using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Internal;

/// <summary>
/// Auto-generated <see cref="QueryDefinition{TEntity}"/> for strongly-typed reference data
/// entity types. Declares the same base columns as <see cref="ReferenceDataQueryDefinition"/>
/// (Code, Labels, Activated, SortOrder, ValidFrom, ValidTo) for any <typeparamref name="TEntity"/>
/// inheriting from <see cref="ReferenceDataEntity"/>.
/// </summary>
internal sealed class GenericReferenceDataQueryDefinition<TEntity>
    : QueryDefinition<TEntity>
    where TEntity : ReferenceDataEntity
{
    /// <inheritdoc/>
    public override string Name => $"ReferenceData.{typeof(TEntity).Name}";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TEntity> builder)
    {
        builder
            .Column(e => e.Code, c => c.Label("Code").Filterable().Sortable())
            .Column(e => e.LabelEn, c => c.Label("Label (EN)").Filterable().Sortable())
            .Column(e => e.LabelFr, c => c.Label("Label (FR)").Filterable())
            .Column(e => e.LabelNl, c => c.Label("Label (NL)").Filterable())
            .Column(e => e.LabelDe, c => c.Label("Label (DE)").Filterable())
            .Column(e => e.LabelHi, c => c.Label("Label (HI)").Filterable())
            .Column(e => e.Activated, c => c.Label("Active").Filterable())
            .Column(e => e.SortOrder, c => c.Label("Sort Order").Sortable())
            .Column(e => e.ValidFrom, c => c.Label("Valid From").Filterable().Sortable())
            .Column(e => e.ValidTo, c => c.Label("Valid To").Filterable().Sortable())
            .Column(e => e.ParentCode, c => c.Label("Parent Code").Filterable());

        builder.GlobalSearch(e => e.Code, e => e.LabelEn, e => e.LabelFr);
        builder.DefaultSort("SortOrder,Code");
    }
}
