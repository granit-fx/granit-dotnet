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
            // Identity
            .Column(u => u.TenantId, c => c.Label("Tenant").LabelKey("Identity.Federated.Columns.Tenant").Filterable().Sortable())
            .Column(u => u.ExternalUserId, c => c.Label("External User ID").LabelKey("Identity.Federated.Columns.ExternalUserId").Filterable().Sortable())
            .Column(u => u.Username, c => c.Label("Username").LabelKey("Identity.Federated.Columns.Username").Filterable().Sortable())
            // Contact (RGPD: PII — admin grid only, gated by *.Read permission)
            .Column(u => u.Email, c => c.Label("Email").LabelKey("Identity.Federated.Columns.Email").Filterable().Sortable())
            .Column(u => u.EmailHash, c => c.Label("Email Hash").LabelKey("Identity.Federated.Columns.EmailHash").Filterable())
            // Profile
            .Column(u => u.FirstName, c => c.Label("First Name").LabelKey("Identity.Federated.Columns.FirstName").Filterable().Sortable())
            .Column(u => u.LastName, c => c.Label("Last Name").LabelKey("Identity.Federated.Columns.LastName").Filterable().Sortable())
            // State
            .Column(u => u.Enabled, c => c.Label("Enabled").LabelKey("Identity.Federated.Columns.Enabled").Filterable().Sortable())
            .Column(u => u.LastSyncedAt, c => c.Label("Last Synced At").LabelKey("Identity.Federated.Columns.LastSyncedAt").Sortable())
            // Audit
            .Column(u => u.CreatedAt, c => c.Label("Created At").LabelKey("Identity.Federated.Columns.CreatedAt").Sortable())
            .Column(u => u.ModifiedAt, c => c.Label("Modified At").LabelKey("Identity.Federated.Columns.ModifiedAt").Sortable())
            .GlobalSearch(u => u.ExternalUserId, u => u.Username, u => u.Email, u => u.FirstName, u => u.LastName)
            .DateFilter(u => u.LastSyncedAt)
            .DefaultSort("-lastSyncedAt")
            .DefaultPageSize(25);
    }
}
