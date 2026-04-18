using Granit.Identity.Federated.Domain;
using Granit.QueryEngine;

namespace Granit.Identity.Federated.Queries;

/// <summary>
/// Query definition for federated user cache entries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class UserCacheEntryQueryDefinition : QueryDefinition<UserCacheEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Identity.Federated.UserCacheEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<UserCacheEntry> builder)
    {
        builder
            .Column(u => u.TenantId, c => c.Label("Tenant").LabelKey("Identity.Federated.Columns.Tenant").Filterable().Sortable())
            .Column(u => u.ExternalUserId, c => c.Label("External User ID").LabelKey("Identity.Federated.Columns.ExternalUserId").Filterable().Sortable())
            .Column(u => u.Enabled, c => c.Label("Enabled").LabelKey("Identity.Federated.Columns.Enabled").Filterable().Sortable())
            .Column(u => u.LastSyncedAt, c => c.Label("Last Synced At").LabelKey("Identity.Federated.Columns.LastSyncedAt").Sortable())
            .Column(u => u.CreatedAt, c => c.Label("Created At").LabelKey("Identity.Federated.Columns.CreatedAt").Sortable())
            .Column(u => u.ModifiedAt, c => c.Label("Modified At").LabelKey("Identity.Federated.Columns.ModifiedAt").Sortable())
            .GlobalSearch(u => u.ExternalUserId)
            .DateFilter(u => u.LastSyncedAt)
            .DefaultSort("-lastSyncedAt")
            .DefaultPageSize(25);
    }
}
