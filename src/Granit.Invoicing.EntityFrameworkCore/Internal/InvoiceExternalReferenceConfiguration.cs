using Granit.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class InvoiceExternalReferenceConfiguration : IEntityTypeConfiguration<InvoiceExternalReference>
{
    public void Configure(EntityTypeBuilder<InvoiceExternalReference> builder)
    {
        builder.ToTable(GranitInvoicingDbProperties.DbTablePrefix + "invoice_external_references", GranitInvoicingDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(256).IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.ExternalId }).IsUnique()
            .HasDatabaseName($"uq_{GranitInvoicingDbProperties.DbTablePrefix}inv_ext_provider_id");
    }
}
