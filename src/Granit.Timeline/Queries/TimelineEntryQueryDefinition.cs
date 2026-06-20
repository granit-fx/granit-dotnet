using Granit.QueryEngine;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Queries;

/// <summary>
/// Query definition for timeline entries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class TimelineEntryQueryDefinition : QueryDefinition<TimelineEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Timeline.TimelineEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TimelineEntry> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("Timeline.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.EntityType, c => c.Label("Entity Type").LabelKey("Timeline.Columns.EntityType").Filterable().Sortable())
            .Column(e => e.EntityId, c => c.Label("Entity ID").LabelKey("Timeline.Columns.EntityId").Filterable().Sortable())
            .Column(e => e.EntryType, c => c.Label("Entry Type").LabelKey("Timeline.Columns.EntryType").Filterable().Sortable())
            .Column(e => e.AuthorId, c => c.Label("Author").LabelKey("Timeline.Columns.AuthorId").Filterable().Sortable().Lookup("users", requiredPermission: "Identity.Users.Read"))
            .Column(e => e.IsDeleted, c => c.Label("Deleted").LabelKey("Timeline.Columns.IsDeleted").Filterable().Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("Timeline.Columns.CreatedAt").Sortable())
            .GlobalSearch(e => e.EntityType, e => e.EntityId, e => e.Body)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(25);
    }
}
