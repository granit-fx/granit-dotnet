using Granit.DataExchange.Export;
using Granit.Documents.Domain;

namespace Granit.Documents.Exports;

/// <summary>CSV / XLSX export whitelist for folders (F11.2).</summary>
public sealed class FolderExportDefinition : ExportDefinition<Folder>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.FolderExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<Folder> builder)
    {
        builder
            .IncludeId()
            .Field(f => f.Name)
            .Field(f => f.Path)
            .Field(f => f.Depth)
            .Field(f => f.ParentFolderId)
            .Field(f => f.OwnerUserId)
            .Field(f => f.IsTenantRoot)
            .Field(f => f.Status)
            .Field(f => f.TrashedAt, fld => fld.Format("O"));
    }
}
