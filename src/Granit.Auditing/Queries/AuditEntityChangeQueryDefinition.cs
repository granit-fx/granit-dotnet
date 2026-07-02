using Granit.Auditing.Domain;
using Granit.Auditing.Dtos;
using Granit.QueryEngine;

namespace Granit.Auditing.Queries;

/// <summary>
/// Query definition for audit entity changes — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class AuditEntityChangeQueryDefinition : QueryDefinition<AuditEntityChange>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Auditing.AuditEntityChangeQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<AuditEntityChange> builder)
    {
        builder
            .Column(e => e.AuditEntryId, c => c.Label("Audit Entry ID").LabelKey("Auditing.Columns.AuditEntryId").Filterable().Sortable())
            .Column(e => e.EntityType, c => c.Label("Entity Type").LabelKey("Auditing.Columns.EntityType").Filterable().Sortable())
            .Column(e => e.EntityId, c => c.Label("Entity ID").LabelKey("Auditing.Columns.EntityId").Filterable())
            .Column(e => e.ChangeType, c => c.Label("Change Type").LabelKey("Auditing.Columns.ChangeType").Filterable().Sortable())
            .AllowGroupBy(e => e.ChangeType)
            .GlobalSearch(e => e.EntityType, e => e.EntityId)
            .DefaultSort("-auditEntryId")
            .DefaultPageSize(25)
            .ProjectTo(e => new AuditEntityChangeSummaryResponse(
                e.Id,
                e.AuditEntryId,
                e.EntityType,
                e.EntityId,
                e.ChangeType,
                e.PropertyChanges.Count));
    }
}
