using Granit.CustomerBalance.Domain;
using Granit.DataExchange.Export;

namespace Granit.CustomerBalance.Exports;

public sealed class BalanceTransactionExportDefinition : ExportDefinition<BalanceTransaction>
{
    public override string Name => "Granit.CustomerBalance.BalanceTransactionExport";

    protected override void Configure(ExportDefinitionBuilder<BalanceTransaction> builder)
    {
        builder
            .IncludeId()
            .Field(t => t.BalanceAccountId)
            .Field(t => t.Type)
            .Field(t => t.Amount, f => f.Format("#,##0.00"))
            .Field(t => t.Source)
            .Field(t => t.Reason)
            .Field(t => t.ReferenceId)
            .Field(t => t.ReferenceType)
            .Field(t => t.ExpiresAt, f => f.Format("O"))
            .Field(t => t.CreatedAt, f => f.Format("O"));
    }
}
