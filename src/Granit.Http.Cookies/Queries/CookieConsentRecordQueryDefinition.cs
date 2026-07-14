using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.Dtos;
using Granit.QueryEngine;

namespace Granit.Http.Cookies.Queries;

/// <summary>
/// Query definition for the cookie-consent ledger — declares columns, filters, sorting,
/// search, and the list-view projection for the query engine. Powers
/// <c>MapGranitQuery&lt;CookieConsentRecord&gt;</c> (consent statistics, DPO evidence review).
/// </summary>
public sealed class CookieConsentRecordQueryDefinition : QueryDefinition<CookieConsentRecord>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Http.Cookies.CookieConsentRecordQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<CookieConsentRecord> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Cookies.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.DecidedAt, c => c.Label("Decided At").LabelKey("Cookies.Columns.DecidedAt").Sortable())
            .Column(e => e.Mode, c => c.Label("Mode").LabelKey("Cookies.Columns.Mode").Filterable().Sortable())
            .Column(e => e.CmpSource, c => c.Label("CMP Source").LabelKey("Cookies.Columns.CmpSource").Filterable().Sortable())
            .Column(e => e.CorrelationId, c => c.Label("Correlation").LabelKey("Cookies.Columns.CorrelationId").Filterable())
            .AllowGroupBy(e => e.Mode)
            .GlobalSearch(e => e.CmpSource, e => e.CorrelationId)
            .DateFilter(e => e.DecidedAt)
            .DefaultSort("-decidedAt")
            .DefaultPageSize(25)
            .ProjectTo(e => new CookieConsentRecordResponse(
                e.Id,
                e.TenantId,
                e.GrantedCategories,
                e.DeniedCategories,
                e.Mode,
                e.CmpSource,
                e.AnonymizedIp,
                e.CorrelationId,
                e.DecidedAt));
    }
}
