using Granit.Auditing.Domain;
using Granit.Auditing.Dtos;
using Granit.QueryEngine;

namespace Granit.Auditing.Queries;

/// <summary>
/// Query definition for audit entries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class AuditEntryQueryDefinition : QueryDefinition<AuditEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Auditing.AuditEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<AuditEntry> builder)
    {
        builder
            .Column(e => e.TenantId, c => c
                .Label("Tenant")
                .LabelKey("Auditing.Columns.Tenant")
                .Filterable()
                .Sortable()
                .Lookup("tenants", requiredPermission: "MultiTenancy.Tenants.Read"))
            .Column(e => e.Timestamp, c => c.Label("Timestamp").LabelKey("Auditing.Columns.Timestamp").Sortable())
            .Column(e => e.Category, c => c.Label("Category").LabelKey("Auditing.Columns.Category").Filterable().Sortable())
            .Column(e => e.UserId, c => c.Label("User ID").LabelKey("Auditing.Columns.UserId").Filterable().Sortable())
            .Column(e => e.UserName, c => c.Label("User Name").LabelKey("Auditing.Columns.UserName").Filterable().Sortable())
            .Column(e => e.IpAddress, c => c.Label("IP Address").LabelKey("Auditing.Columns.IpAddress").Filterable())
            .Column(e => e.CorrelationId, c => c.Label("Correlation ID").LabelKey("Auditing.Columns.CorrelationId").Filterable())
            .GlobalSearch(e => e.UserId, e => e.UserName, e => e.CorrelationId)
            .DateFilter(e => e.Timestamp)
            .DefaultSort("-timestamp")
            .DefaultPageSize(25)
            .ProjectTo(e => new AuditEntryResponse(
                e.Id,
                e.Timestamp,
                e.UserId,
                e.UserName,
                e.Category,
                e.IpAddress,
                e.TenantId,
                e.CorrelationId,
                e.EntityChanges.Count));
    }
}
