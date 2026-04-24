using Granit.Catalog.Domain;
using Granit.QueryEngine;

namespace Granit.Catalog.Queries;

/// <summary>
/// Query definition for catalog products — declares columns, filters, sorting,
/// and search for the query engine. Powers the paginated /catalog/products
/// admin grid (filterable, sortable, exportable).
/// </summary>
public sealed class ProductQueryDefinition : QueryDefinition<Product>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Catalog.ProductQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Product> builder)
    {
        builder
            .Column(p => p.Sku, c => c.Label("SKU").LabelKey("Catalog.Columns.Sku").Filterable().Sortable())
            .Column(p => p.Name, c => c.Label("Name").LabelKey("Catalog.Columns.Name").Filterable().Sortable())
            .Column(p => p.Description, c => c.Label("Description").LabelKey("Catalog.Columns.Description").Filterable())
            .Column(p => p.Type, c => c.Label("Type").LabelKey("Catalog.Columns.Type").Filterable().Sortable())
            .Column(p => p.Unit, c => c.Label("Unit").LabelKey("Catalog.Columns.Unit").Filterable().Sortable())
            .Column(p => p.LifecycleStatus, c => c.Label("Lifecycle Status").LabelKey("Catalog.Columns.LifecycleStatus").Filterable().Sortable())
            .Column(p => p.CreatedAt, c => c.Label("Created At").LabelKey("Catalog.Columns.CreatedAt").Sortable())
            .Column(p => p.ModifiedAt, c => c.Label("Modified At").LabelKey("Catalog.Columns.ModifiedAt").Sortable())
            .GlobalSearch(p => p.Sku, p => p.Name, p => p.Description)
            .DateFilter(p => p.CreatedAt)
            .DefaultSort("sku")
            .DefaultPageSize(25);
    }
}
