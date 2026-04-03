using Granit.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class InvoiceDocumentConfiguration : IEntityTypeConfiguration<InvoiceDocument>
{
    public void Configure(EntityTypeBuilder<InvoiceDocument> builder)
    {
        builder.ToTable(GranitInvoicingDbProperties.DbTablePrefix + "invoice_documents", GranitInvoicingDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.GeneratedAt).IsRequired();
    }
}
