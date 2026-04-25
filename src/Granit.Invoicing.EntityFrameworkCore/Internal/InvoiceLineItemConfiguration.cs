using Granit.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable(GranitInvoicingDbProperties.DbTablePrefix + "invoice_line_items", GranitInvoicingDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.TaxRate).HasPrecision(8, 6);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.SourceType).IsRequired();
        builder.Property(e => e.SourceId).HasMaxLength(256);
        builder.Property(e => e.ProductId);
        builder.HasIndex(e => e.ProductId);
    }
}
