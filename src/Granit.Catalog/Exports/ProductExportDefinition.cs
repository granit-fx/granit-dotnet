using Granit.Catalog.Domain;
using Granit.DataExchange.Export;

namespace Granit.Catalog.Exports;

/// <summary>
/// Export definition for catalog products — whitelist of fields surfaced in
/// CSV / XLSX exports, with formatting hints for date columns.
/// </summary>
public sealed class ProductExportDefinition : ExportDefinition<Product>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Catalog.ProductExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Product> builder)
    {
        builder
            .IncludeId()
            .Field(p => p.Sku)
            .Field(p => p.Name)
            .Field(p => p.Description)
            .Field(p => p.Type)
            .Field(p => p.Unit)
            .Field(p => p.LifecycleStatus)
            .Field(p => p.CreatedAt, f => f.Format("O"))
            .Field(p => p.CreatedBy)
            .Field(p => p.ModifiedAt, f => f.Format("O"))
            .Field(p => p.ModifiedBy);
    }
}
