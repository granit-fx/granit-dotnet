using Granit.Identity.Federated.Domain;
using Granit.QueryEngine;

namespace Granit.Identity.Federated.Queries;

/// <summary>
/// Query definition for federated user cache entries — declares columns, filters, sorting,
/// and search for the query engine.
/// </summary>
public sealed class FederatedIdentityQueryDefinition : QueryDefinition<FederatedIdentity>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Identity.Federated.FederatedIdentityQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<FederatedIdentity> builder)
    {
        // Username, Email, FirstName, LastName are all [Encrypted] (random-IV AES): the
        // ciphertext is non-deterministic, so LIKE/equality filters silently match nothing
        // and sorts order by ciphertext. They stay projectable (shown in the grid, gated by
        // *.Read) but are NOT filterable/sortable/searchable. Exact-match email lookup is
        // served by the store via EmailHash, which is never surfaced as a grid column.
        // (audit ARCHITECTURE #1)
        builder
            // Identity
            .Column(u => u.TenantId, c => c.Label("Tenant").LabelKey("Identity.Federated.Columns.Tenant").Filterable().Sortable())
            .Column(u => u.ExternalUserId, c => c.Label("External User ID").LabelKey("Identity.Federated.Columns.ExternalUserId").Filterable().Sortable())
            .Column(u => u.Username, c => c.Label("Username").LabelKey("Identity.Federated.Columns.Username"))
            // Contact (RGPD: PII — admin grid only, gated by *.Read permission)
            .Column(u => u.Email, c => c.Label("Email").LabelKey("Identity.Federated.Columns.Email"))
            // Profile
            .Column(u => u.FirstName, c => c.Label("First Name").LabelKey("Identity.Federated.Columns.FirstName"))
            .Column(u => u.LastName, c => c.Label("Last Name").LabelKey("Identity.Federated.Columns.LastName"))
            // State
            .Column(u => u.Enabled, c => c.Label("Enabled").LabelKey("Identity.Federated.Columns.Enabled").Filterable().Sortable())
            .Column(u => u.LastSyncedAt, c => c.Label("Last Synced At").LabelKey("Identity.Federated.Columns.LastSyncedAt").Sortable())
            // Audit
            .Column(u => u.CreatedAt, c => c.Label("Created At").LabelKey("Identity.Federated.Columns.CreatedAt").Sortable())
            .Column(u => u.ModifiedAt, c => c.Label("Modified At").LabelKey("Identity.Federated.Columns.ModifiedAt").Sortable())
            .AllowGroupBy(u => u.Enabled)
            .GlobalSearch(u => u.ExternalUserId)
            .DateFilter(u => u.LastSyncedAt)
            .DefaultSort("-lastSyncedAt")
            .DefaultPageSize(25);
    }
}
