using Granit.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable(GranitInvoicingDbProperties.DbTablePrefix + "invoices", GranitInvoicingDbProperties.DbSchema);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DocumentType).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.CollectionMethod).IsRequired();
        builder.Property(e => e.BillingReason).IsRequired();
        builder.Property(e => e.CreditNoteReason).HasMaxLength(500);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Subtotal).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.TaxTotal).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.AmountPaid).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.AmountCredited).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.AmountRemaining).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Overpayment).HasPrecision(18, 4).IsRequired();

        // ParentInvoiceId and PartyId are SingleValueObject<Guid> — declared as scalar
        // properties to prevent EF Core from discovering them as navigations. The value
        // converter is applied automatically by ApplyGranitConventions.
        builder.Property(e => e.ParentInvoiceId);
        builder.Property(e => e.PartyId).IsRequired();

        builder.OwnsOne(e => e.IssuedBillingAddressSnapshot, ba =>
        {
            ba.Property(a => a.CompanyName).HasMaxLength(200);
            ba.Property(a => a.Line1).HasMaxLength(200).IsRequired();
            ba.Property(a => a.Line2).HasMaxLength(200);
            ba.Property(a => a.City).HasMaxLength(100).IsRequired();
            ba.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
            ba.Property(a => a.State).HasMaxLength(100);
            ba.Property(a => a.Country).HasMaxLength(2).IsRequired();
            ba.Property(a => a.VatNumber).HasMaxLength(30);
        });

        builder.HasMany(e => e.LineItems).WithOne().HasForeignKey("InvoiceId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.ExternalReferences).WithOne().HasForeignKey("InvoiceId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Documents).WithOne().HasForeignKey("InvoiceId").OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.InvoiceNumber).IsUnique()
            .HasFilter("\"InvoiceNumber\" IS NOT NULL")
            .HasDatabaseName($"uq_{GranitInvoicingDbProperties.DbTablePrefix}invoices_number");

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName($"ix_{GranitInvoicingDbProperties.DbTablePrefix}invoices_tenant_status");

        builder.HasIndex(e => e.PartyId)
            .HasDatabaseName($"ix_{GranitInvoicingDbProperties.DbTablePrefix}invoices_contact");

        builder.HasIndex(e => e.DueAt)
            .HasFilter("\"Status\" = 1")
            .HasDatabaseName($"ix_{GranitInvoicingDbProperties.DbTablePrefix}invoices_due_at_open");

        builder.HasIndex(e => e.ParentInvoiceId)
            .HasFilter("\"ParentInvoiceId\" IS NOT NULL")
            .HasDatabaseName($"ix_{GranitInvoicingDbProperties.DbTablePrefix}invoices_parent");
    }
}
