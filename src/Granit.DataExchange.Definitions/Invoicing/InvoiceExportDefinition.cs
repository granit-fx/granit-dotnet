using Granit.DataExchange.Export;
using Granit.Invoicing.Domain;

namespace Granit.DataExchange.Definitions.Invoicing;

public sealed class InvoiceExportDefinition : ExportDefinition<Invoice>
{
    private const string MoneyFormat = "#,##0.00";

    public override string Name => "Granit.Invoicing.InvoiceExport";

    protected override void Configure(ExportDefinitionBuilder<Invoice> builder)
    {
        builder
            .IncludeId()
            .Field(i => i.DocumentType)
            .Field(i => i.InvoiceNumber)
            .Field(i => i.Status)
            .Field(i => i.CollectionMethod)
            .Field(i => i.BillingReason)
            .Field(i => i.Currency)
            .Field(i => i.Subtotal, f => f.Format(MoneyFormat))
            .Field(i => i.TaxTotal, f => f.Format(MoneyFormat))
            .Field(i => i.Total, f => f.Format(MoneyFormat))
            .Field(i => i.AmountPaid, f => f.Format(MoneyFormat))
            .Field(i => i.AmountCredited, f => f.Format(MoneyFormat))
            .Field(i => i.AmountRemaining, f => f.Format(MoneyFormat))
            .Field(i => i.Overpayment, f => f.Format(MoneyFormat))
            .Field(i => i.IssuedAt, f => f.Format("O"))
            .Field(i => i.DueAt, f => f.Format("O"))
            .Field(i => i.PaidAt, f => f.Format("O"))
            .Field(i => i.PeriodStart, f => f.Format("O"))
            .Field(i => i.PeriodEnd, f => f.Format("O"))
            .Field(i => i.IsCreditNote)
            .Field(i => i.TenantId)
            .Field(i => i.CreatedAt, f => f.Format("O"))
            .Field(i => i.CreatedBy)
            .Field(i => i.ModifiedAt, f => f.Format("O"))
            .Field(i => i.ModifiedBy);
    }
}
