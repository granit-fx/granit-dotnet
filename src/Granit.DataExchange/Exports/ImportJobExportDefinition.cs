using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Domain;

namespace Granit.DataExchange.Exports;

public sealed class ImportJobExportDefinition : ExportDefinition<ImportJob>
{
    public override string Name => "Granit.DataExchange.ImportJobExport";

    protected override void Configure(ExportDefinitionBuilder<ImportJob> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.DefinitionName)
            .Field(e => e.EntityTypeName)
            .Field(e => e.OriginalFileName)
            .Field(e => e.MimeType)
            .Field(e => e.FileSizeBytes)
            .Field(e => e.BlobReference)
            .Field(e => e.Status)
            .Field(e => e.CompletedAt, f => f.Format("O"))
            .Field(e => e.TenantId)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
