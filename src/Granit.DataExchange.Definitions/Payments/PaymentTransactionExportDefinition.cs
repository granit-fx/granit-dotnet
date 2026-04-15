using Granit.DataExchange.Export;
using Granit.Payments.Domain;

namespace Granit.DataExchange.Definitions.Payments;

public sealed class PaymentTransactionExportDefinition : ExportDefinition<PaymentTransaction>
{
    public override string Name => "Granit.Payments.PaymentTransactionExport";

    protected override void Configure(ExportDefinitionBuilder<PaymentTransaction> builder)
    {
        builder
            .IncludeId()
            .Field(t => t.InvoiceId)
            .Field(t => t.Amount, f => f.Format("#,##0.00"))
            .Field(t => t.Currency)
            .Field(t => t.Status)
            .Field(t => t.ProviderName)
            .Field(t => t.MethodType)
            .Field(t => t.ProviderTransactionId)
            .Field(t => t.PaymentMethodId)
            .Field(t => t.IdempotencyKey)
            .Field(t => t.SucceededAt, f => f.Format("O"))
            .Field(t => t.CanceledAt, f => f.Format("O"))
            .Field(t => t.TenantId)
            .Field(t => t.CreatedAt, f => f.Format("O"))
            .Field(t => t.CreatedBy)
            .Field(t => t.ModifiedAt, f => f.Format("O"))
            .Field(t => t.ModifiedBy);
    }
}
