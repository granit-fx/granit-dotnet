using Granit.DataExchange.Export;
using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.Exports;

/// <summary>
/// Export definition for the cookie-consent ledger. Mirrors the list-view columns and
/// reuses <see cref="Queries.CookieConsentRecordQueryDefinition"/> so exports honour the
/// same filtering and sorting pipeline as the grid — the DPO's Art. 7(1) evidence extract.
/// </summary>
public sealed class CookieConsentRecordExportDefinition : ExportDefinition<CookieConsentRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Http.Cookies.CookieConsentRecordExport";

    /// <inheritdoc/>
    public override string? QueryDefinitionName => "Granit.Http.Cookies.CookieConsentRecordQuery";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<CookieConsentRecord> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.DecidedAt, f => f.Format("O"))
            .Field(e => e.Mode)
            .Field(e => e.CmpSource)
            .Field(e => e.AnonymizedIp)
            .Field(e => e.CorrelationId)
            .Field(e => e.TenantId)
            .IncludeCreationAuditFields()
            .ComplexField("GrantedCategories", e => string.Join(", ", e.GrantedCategories))
            .ComplexField("DeniedCategories", e => string.Join(", ", e.DeniedCategories));
    }
}
