using Granit.Entities;
using Granit.Entities.Layouts;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Exports;
using Granit.Invoicing.Metrics;
using Granit.Invoicing.Queries;

namespace Granit.Invoicing.Entities;

/// <summary>
/// Phase 1.F cobaye — declares the <see cref="Invoice"/> aggregate's UI surface
/// (per ADR-040). Form variants <c>"default"</c> + <c>"quick"</c>; the default
/// form carries the line-items, external-references, and document
/// owned-collection sections; detail uses the standard Audit + Timeline side
/// panels; list collection backed by the existing <c>InvoiceQueryDefinition</c>.
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
                    .Field(i => i.AmountRemaining))
                .OwnedCollectionSection<InvoiceLineItem>("lineItems", i => i.LineItems, s => s
                    .ItemDisplayProperty(li => li.Description)
                    .ItemField(li => li.Description)
                    .ItemField(li => li.Quantity)
                    .ItemField(li => li.UnitPrice)
                    .ItemField(li => li.Amount, x => x.ReadOnly())
                    .ItemField(li => li.TaxRate)
                    .ItemField(li => li.TaxAmount, x => x.ReadOnly()))
                .OwnedCollectionSection<InvoiceExternalReference>(
                    "externalReferences", i => i.ExternalReferences, s => s
                    .CollapsedByDefault()
                    .ItemDisplayProperty(r => r.ProviderName)
                    .ItemField(r => r.ProviderName)
                    .ItemField(r => r.ExternalId))
                .OwnedCollectionSection<InvoiceDocument>("documents", i => i.Documents, s => s
                    .CollapsedByDefault()
                    .ItemDisplayProperty(doc => doc.FileName)
                    .ItemField(doc => doc.FileName, x => x.ReadOnly())
                    .ItemField(doc => doc.ContentType, x => x.ReadOnly())
                    .ItemField(doc => doc.GeneratedAt, x => x.ReadOnly())))
            .Form("quick", f => f
                .Section("essentials", s => s
                    .Field(i => i.PartyId)
                    .Field(i => i.Currency)
                    .Field(i => i.DueAt)))
            .Detail("default", d =>
            {
                d.Section("overview", s => s.InheritsFromForm("default"));
                d.SidePanel.Audit().Timeline();
            })
            // Phase 2.A — kanban grouped by InvoiceStatus. Tile shows the
            // invoice number as headline + party + due date + total in the
            // body. Terminal states (Void / Uncollectible) are hidden by
            // default — workflow keeps them addressable but they shouldn't
            // clutter the daily board.
            .KanbanView<InvoiceStatus>(k => k
                .GroupBy(i => i.Status)
                .Card(c => c
                    .Title(i => i.InvoiceNumber)
                    .Field(i => i.PartyId)
                    .Field(i => i.DueAt)
                    .Field(i => i.Total))
                .Column(InvoiceStatus.Draft, c => c.Color(KanbanColor.Gray))
                .Column(InvoiceStatus.Open, c => c.Color(KanbanColor.Orange))
                .Column(InvoiceStatus.Paid, c => c.Color(KanbanColor.Green))
                .Column(InvoiceStatus.Void, c => c.Color(KanbanColor.Neutral).Hidden())
                .Column(InvoiceStatus.Uncollectible, c => c.Color(KanbanColor.Red).Collapsed()));
}
