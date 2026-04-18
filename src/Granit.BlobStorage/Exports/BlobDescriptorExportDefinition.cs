using Granit.BlobStorage.Domain;
using Granit.DataExchange.Export;

namespace Granit.BlobStorage.Exports;

public sealed class BlobDescriptorExportDefinition : ExportDefinition<BlobDescriptor>
{
    public override string Name => "Granit.BlobStorage.BlobDescriptorExport";

    protected override void Configure(ExportDefinitionBuilder<BlobDescriptor> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.ContainerName)
            .Field(e => e.ObjectKey)
            .Field(e => e.OriginalFileName)
            .Field(e => e.DeclaredContentType)
            .Field(e => e.MaxAllowedBytes)
            .Field(e => e.VerifiedContentType)
            .Field(e => e.SizeBytes)
            .Field(e => e.Status)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.ValidatedAt, f => f.Format("O"))
            .Field(e => e.DeletedAt, f => f.Format("O"))
            .Field(e => e.RejectionReason)
            .Field(e => e.DeletionReason)
            .Field(e => e.TenantId);
    }
}
