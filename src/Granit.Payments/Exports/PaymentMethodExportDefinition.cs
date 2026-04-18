using Granit.DataExchange.Export;
using Granit.Payments.Domain;

namespace Granit.Payments.Exports;

public sealed class PaymentMethodExportDefinition : ExportDefinition<PaymentMethod>
{
    public override string Name => "Granit.Payments.PaymentMethodExport";

    protected override void Configure(ExportDefinitionBuilder<PaymentMethod> builder)
    {
        builder
            .IncludeId()
            .Field(m => m.Type)
            .Field(m => m.ProviderName)
            .Field(m => m.IsDefault)
            .Field(m => m.ExpiresAt, f => f.Format("O"))
            .Field(m => m.TenantId)
            .Field(m => m.CreatedAt, f => f.Format("O"))
            .Field(m => m.CreatedBy)
            .Field(m => m.ModifiedAt, f => f.Format("O"))
            .Field(m => m.ModifiedBy);
    }
}
