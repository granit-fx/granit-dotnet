using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Dtos;
using Granit.QueryEngine;

namespace Granit.Authentication.ApiKeys.Queries;

/// <summary>
/// Query definition for API keys — declares columns, filters, sorting, search, and the
/// list-view projection for the query engine. Powers <c>MapGranitQuery&lt;ApiKeyEntry&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Revocation visibility is modelled with two quick filters so the security default
/// (hide revoked keys) is preserved while still allowing an explicit opt-in:
/// </para>
/// <list type="bullet">
/// <item><c>active</c> (default): only non-revoked keys — applied automatically when the
/// caller specifies no quick filters.</item>
/// <item><c>includeRevoked</c>: a no-op predicate whose sole purpose is to <em>replace</em>
/// the default <c>active</c> filter, yielding the full set (active + revoked). Request it
/// via <c>?quickFilters=includeRevoked</c>.</item>
/// </list>
/// </remarks>
public sealed class ApiKeyEntryQueryDefinition : QueryDefinition<ApiKeyEntry>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Authentication.ApiKeys.ApiKeyEntryQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<ApiKeyEntry> builder)
    {
        builder
            .Column(e => e.TenantId, c => c.Label("Tenant").LabelKey("ApiKeys.Columns.Tenant").Filterable().Sortable())
            .Column(e => e.Name, c => c.Label("Name").LabelKey("ApiKeys.Columns.Name").Filterable().Sortable())
            .Column(e => e.Type, c => c.Label("Type").LabelKey("ApiKeys.Columns.Type").Filterable().Sortable())
            .Column(e => e.Environment, c => c.Label("Environment").LabelKey("ApiKeys.Columns.Environment").Filterable().Sortable())
            .Column(e => e.Prefix, c => c.Label("Prefix").LabelKey("ApiKeys.Columns.Prefix").Filterable())
            .Column(e => e.LastFourChars, c => c.Label("Last Four").LabelKey("ApiKeys.Columns.LastFourChars"))
            .Column(e => e.ExpiresAt, c => c.Label("Expires At").LabelKey("ApiKeys.Columns.ExpiresAt").Sortable())
            .Column(e => e.LastUsedAt, c => c.Label("Last Used At").LabelKey("ApiKeys.Columns.LastUsedAt").Sortable())
            .Column(e => e.RevokedAt, c => c.Label("Revoked At").LabelKey("ApiKeys.Columns.RevokedAt").Sortable())
            .Column(e => e.CreatedAt, c => c.Label("Created At").LabelKey("ApiKeys.Columns.CreatedAt").Sortable())
            .AllowGroupBy(e => e.Type)
            .QuickFilter(
                "active",
                "Active keys only",
                e => e.RevokedAt == null,
                isDefault: true)
            .QuickFilter(
                "includeRevoked",
                "Include revoked keys",
                _ => true)
            .GlobalSearch(e => e.Name)
            .DateFilter(e => e.CreatedAt)
            .DefaultSort("-createdAt")
            .DefaultPageSize(20)
            .MaxPageSize(100)
            .ProjectTo(e => new ApiKeyListItemResponse(
                e.Id,
                e.Name,
                e.Type,
                e.Environment,
                e.Prefix,
                e.LastFourChars,
                e.ExpiresAt,
                e.LastUsedAt,
                e.RevokedAt,
                e.CacheBehavior,
                e.CreatedAt));
    }
}
