using Granit.DataExchange.Export;
using Granit.Documents.Domain;

namespace Granit.Documents.Exports;

/// <summary>CSV / XLSX export whitelist for documents (F11.1).</summary>
public sealed class DocumentExportDefinition : ExportDefinition<Document>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.DocumentExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Document> builder)
    {
        builder
            .IncludeId()
            .Field(d => d.Name)
            .Field(d => d.Description)
            .Field(d => d.FolderId)
            .Field(d => d.OwnerUserId)
            .Field(d => d.Status)
            .Field(d => d.CurrentVersionId)
            .Field(d => d.TrashedAt, f => f.Format("O"));
    }
}
