using Granit.DataExchange.Export;
using Granit.Payments.Domain;

namespace Granit.DataExchange.Definitions.Payments;

public sealed class DisputeExportDefinition : ExportDefinition<Dispute>
{
    public override string Name => "Granit.Payments.DisputeExport";

    protected override void Configure(ExportDefinitionBuilder<Dispute> builder)
    {
        builder
            .IncludeId()
            .Field(e => e.ProviderDisputeId)
            .Field(e => e.Status)
            .Field(e => e.Reason)
            .Field(e => e.Amount, f => f.Format("#,##0.00"))
            .Field(e => e.Currency)
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.ResolvedAt, f => f.Format("O"));
    }
}
