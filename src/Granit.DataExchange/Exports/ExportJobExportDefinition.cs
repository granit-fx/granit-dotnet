using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;

namespace Granit.DataExchange.Exports;

public sealed class ExportJobExportDefinition : ExportDefinition<ExportJob>
{
    public override string Name => "Granit.DataExchange.ExportJobExport";

    protected override void Configure(ExportDefinitionBuilder<ExportJob> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.DefinitionName)
            .Field(e => e.Format)
            .Field(e => e.Status)
            .Field(e => e.BlobReference)
            .Field(e => e.FileName)
            .Field(e => e.RowCount)
            .Field(e => e.ErrorMessage)
            .Field(e => e.CompletedAt, f => f.Format("O"))
            .Field(e => e.TenantId)
            .IncludeAuditFields();
    }
}
