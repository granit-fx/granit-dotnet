using Granit.DataExchange.Export;
using Granit.Identity.Federated.Domain;

namespace Granit.Identity.Federated.Exports;

public sealed class UserCacheEntryExportDefinition : ExportDefinition<UserCacheEntry>
{
    public override string Name => "Granit.Identity.Federated.UserCacheEntryExport";

    protected override void Configure(ExportDefinitionBuilder<UserCacheEntry> builder)
    {
        builder
            .IncludeId()
            .Field(u => u.ExternalUserId)
            .Field(u => u.Enabled)
            .Field(u => u.LastSyncedAt, f => f.Format("O"))
            .Field(u => u.TenantId)
            .Field(u => u.CreatedAt, f => f.Format("O"))
            .Field(u => u.CreatedBy)
            .Field(u => u.ModifiedAt, f => f.Format("O"))
            .Field(u => u.ModifiedBy);
    }
}
