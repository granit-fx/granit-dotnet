using Granit.DataExchange.Export;
using Granit.Documents.Domain;

namespace Granit.Documents.Exports;

/// <summary>CSV / XLSX export whitelist for tenant storage quotas (F11).</summary>
public sealed class TenantStorageQuotaExportDefinition : ExportDefinition<TenantStorageQuota>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.TenantStorageQuotaExport";

    /// <inheritdoc/>
    protected override void Configure(ExportDefinitionBuilder<TenantStorageQuota> builder)
    {
        builder
            .IncludeId()
            .Field(q => q.TenantId)
            .Field(q => q.LimitBytes)
            .Field(q => q.UsageBytes)
            .Field(q => q.UpdatedAt, f => f.Format("O"));
    }
}
