using Granit.DataExchange.Export;
using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.Exports;

/// <summary>
/// Export definition for legal documents — pairs with <see cref="Queries.LegalDocumentQueryDefinition"/>
/// (ADR-020) so the admin grid's current view can be exported to CSV/XLSX with the same columns.
/// </summary>
public sealed class LegalDocumentExportDefinition : ExportDefinition<LegalDocument>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Privacy.LegalDocumentExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<LegalDocument> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.DocumentId)
            .Field(e => e.DisplayName)
            .Field(e => e.Version)
            .Field(e => e.LifecycleStatus)
            .Field(e => e.Description)
            .Field(e => e.TemplateName)
            .Field(e => e.DocumentBlobId)
            .Field(e => e.TenantId)
            .IncludeAuditFields();
    }
}
