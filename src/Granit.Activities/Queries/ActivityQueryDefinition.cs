using Granit.Activities.Domain;
using Granit.Activities.Internal;
using Granit.QueryEngine;

namespace Granit.Activities.Queries;

/// <summary>
/// Query definition for activities — declares the columns, filters, sorting,
/// and search exposed by the admin grid endpoint backing the cross-entity
/// activities list.
/// </summary>
public sealed class ActivityQueryDefinition : QueryDefinition<Activity>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Activities.ActivityQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(ActivitiesLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Activity> builder)
    {
        builder
            // ── Tenant + polymorphic host ────────────────────────────────────
            .Column(a => a.TenantId, c => c.Label("Tenant").LabelKey("Activities.Columns.Tenant").Filterable().Sortable())
            .Column(a => a.EntityType, c => c.Label("Host Entity").LabelKey("Activities.Columns.EntityType").Filterable().Sortable())
            .Column(a => a.EntityId, c => c.Label("Host Id").LabelKey("Activities.Columns.EntityId").Filterable())
            // ── Type + lifecycle ─────────────────────────────────────────────
            .Column(a => a.Type, c => c.Label("Type").LabelKey("Activities.Columns.Type").Filterable().Sortable())
            .Column(a => a.Status, c => c.Label("Status").LabelKey("Activities.Columns.Status").Filterable().Sortable())
            // ── Assignment ───────────────────────────────────────────────────
            .Column(a => a.AssignedToUserId, c => c.Label("Assigned To").LabelKey("Activities.Columns.AssignedToUserId").Filterable())
            .Column(a => a.CreatedByUserId, c => c.Label("Created By").LabelKey("Activities.Columns.CreatedByUserId").Filterable())
            .Column(a => a.CompletedByUserId, c => c.Label("Completed By").LabelKey("Activities.Columns.CompletedByUserId").Filterable())
            // ── Time ─────────────────────────────────────────────────────────
            .Column(a => a.DueAt, c => c.Label("Due At").LabelKey("Activities.Columns.DueAt").Sortable())
            .Column(a => a.CompletedAt, c => c.Label("Completed At").LabelKey("Activities.Columns.CompletedAt").Sortable())
            .Column(a => a.OverdueNotifiedAt, c => c.Label("Overdue Notified At").LabelKey("Activities.Columns.OverdueNotifiedAt").Sortable())
            // ── Free text ────────────────────────────────────────────────────
            .Column(a => a.Description, c => c.Label("Description").LabelKey("Activities.Columns.Description").Filterable())
            .GlobalSearch(a => a.Description)
            .DateFilter(a => a.DueAt)
            .DefaultSort("-dueAt")
            .DefaultPageSize(25);
    }
}
