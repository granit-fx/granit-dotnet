using Granit.DataExchange.Export;
using Granit.QueryEngine.SavedViews.Domain;

namespace Granit.QueryEngine.Exports;

public sealed class SavedViewExportDefinition : ExportDefinition<SavedView>
{
    public override string Name => "Granit.QueryEngine.SavedViewExport";

    protected override void Configure(ExportDefinitionBuilder<SavedView> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.EntityType)
            .Field(e => e.Name)
            .Field(e => e.UserId)
            .Field(e => e.IsShared)
            .Field(e => e.IsDefault)
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
