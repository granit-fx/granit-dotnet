using Granit.DataExchange.Export;
using Granit.Payments.Domain;

namespace Granit.DataExchange.Definitions.Payments;

public sealed class RefundExportDefinition : ExportDefinition<Refund>
{
    public override string Name => "Granit.Payments.RefundExport";

    protected override void Configure(ExportDefinitionBuilder<Refund> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.Amount, f => f.Format("#,##0.00"))
            .Field(e => e.Currency)
            .Field(e => e.Status)
            .Field(e => e.ProviderRefundId)
            .Field(e => e.Reason)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CompletedAt, f => f.Format("O"));
    }
}
