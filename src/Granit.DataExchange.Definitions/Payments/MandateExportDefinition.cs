using Granit.DataExchange.Export;
using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.DataExchange.Definitions.Payments;

public sealed class MandateExportDefinition : ExportDefinition<Mandate>
{
    public override string Name => "Granit.Payments.SepaDirectDebit.MandateExport";

    protected override void Configure(ExportDefinitionBuilder<Mandate> builder)
    {
        builder
            .IncludeId()
            .Field(m => m.MandateReference)
            .Field(m => m.Status)
            .Field(m => m.Scheme)
            .Field(m => m.CreditorId)
            .Field(m => m.SignedAt, f => f.Format("O"))
            .Field(m => m.ActivatedAt, f => f.Format("O"))
            .Field(m => m.CancelledAt, f => f.Format("O"))
            .Field(m => m.LastCollectionAt, f => f.Format("O"))
            .Field(m => m.ProviderName)
            .Field(m => m.ProviderMandateId)
            .Field(m => m.TenantId)
            .Field(m => m.CreatedAt, f => f.Format("O"))
            .Field(m => m.CreatedBy)
            .Field(m => m.ModifiedAt, f => f.Format("O"))
            .Field(m => m.ModifiedBy);
    }
}
