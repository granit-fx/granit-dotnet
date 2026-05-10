using Granit.Documents.Domain;
using Granit.Documents.Internal;
using Granit.QueryEngine;

namespace Granit.Documents.Queries;

/// <summary>
/// Query definition for the per-tenant storage-quota grid (F11). Surfaces the
/// usage / limit / last-update fields powering the F7 quota admin view.
/// </summary>
public sealed class TenantStorageQuotaQueryDefinition : QueryDefinition<TenantStorageQuota>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.TenantStorageQuotaQuery";

    /// <inheritdoc/>
    public override Type? LocalizationResourceType => typeof(DocumentsLocalizationResource);

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<TenantStorageQuota> builder)
    {
        builder
            .Column(q => q.TenantId, c => c.Label("Tenant").LabelKey("Documents.Columns.Quota.Tenant").Filterable().Sortable())
            .Column(q => q.LimitBytes, c => c.Label("Limit (bytes)").LabelKey("Documents.Columns.Quota.LimitBytes").Sortable())
            .Column(q => q.UsageBytes, c => c.Label("Usage (bytes)").LabelKey("Documents.Columns.Quota.UsageBytes").Sortable())
            .Column(q => q.UpdatedAt, c => c.Label("Updated At").LabelKey("Documents.Columns.Quota.UpdatedAt").Sortable())
            .DefaultSort("-usageBytes")
            .DefaultPageSize(25);
    }
}
