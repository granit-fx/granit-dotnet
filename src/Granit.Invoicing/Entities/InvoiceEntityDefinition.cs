using Granit.Entities;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Exports;
using Granit.Invoicing.Metrics;
using Granit.Invoicing.Queries;

namespace Granit.Invoicing.Entities;

/// <summary>
/// Phase 1.F cobaye — declares the <see cref="Invoice"/> aggregate's UI surface
/// (per ADR-040). Form variants <c>"default"</c> + <c>"quick"</c>, detail with
/// the standard Audit + Timeline side panels, list collection backed by the
/// existing <c>InvoiceQueryDefinition</c>.
/// </summary>
public sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
{
    /// <inheritdoc />
    public override string Name => "Granit.Invoicing.Invoice";

    /// <inheritdoc />
    protected override void Configure(EntityDefinitionBuilder<Invoice> builder) =>
        builder
            .DisplayKey("Invoicing:Entity.Invoice")
            .Icon("file-text")
            .PermissionGroup("Invoicing.Invoices")
            .DisplayProperty(i => i.InvoiceNumber)
            .Query<InvoiceQueryDefinition>()
            .Export<InvoiceExportDefinition>()
            .Metric<UnpaidInvoiceCountMetricDefinition>()
            .Metric<UnpaidInvoiceTotalMetricDefinition>()
            .Metric<OverdueInvoiceCountMetricDefinition>()
            .Form("default", f => f
                .Section("identity", s => s
                    .Field(i => i.InvoiceNumber)
                    .Field(i => i.DocumentType)
                    .Field(i => i.Status)
                    .Field(i => i.PartyId))
                .Section("billing", s => s
                    .Field(i => i.BillingReason)
                    .Field(i => i.CollectionMethod)
                    .Field(i => i.Currency)
                    .Field(i => i.IssuedAt)
                    .Field(i => i.DueAt))
                .Section("amounts", s => s
                    .Field(i => i.Subtotal)
                    .Field(i => i.TaxTotal)
                    .Field(i => i.Total)
                    .Field(i => i.AmountPaid)
                    .Field(i => i.AmountRemaining)))
            .Form("quick", f => f
                .Section("essentials", s => s
                    .Field(i => i.PartyId)
                    .Field(i => i.Currency)
                    .Field(i => i.DueAt)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            });
}
