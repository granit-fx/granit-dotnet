using Granit.CustomerBalance.Domain;
using Granit.DataExchange.Export;

namespace Granit.CustomerBalance.Exports;

public sealed class BalanceAccountExportDefinition : ExportDefinition<BalanceAccount>
{
    public override string Name => "Granit.CustomerBalance.BalanceAccountExport";

    protected override void Configure(ExportDefinitionBuilder<BalanceAccount> builder)
    {
        builder
            .IncludeId()
            .Field(a => a.Currency)
            .Field(a => a.Balance, f => f.Format("#,##0.00"))
            .Field(a => a.TenantId)
            .Field(a => a.CreatedAt, f => f.Format("O"))
            .Field(a => a.CreatedBy)
            .Field(a => a.ModifiedAt, f => f.Format("O"))
            .Field(a => a.ModifiedBy);
    }
}
