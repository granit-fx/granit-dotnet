using Granit.Contacts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Contacts.EntityFrameworkCore.Internal;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable(
            GranitContactsDbProperties.DbTablePrefix + "contacts",
            GranitContactsDbProperties.DbSchema);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Kind).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Website).HasMaxLength(2048);
        builder.Property(c => c.Language).HasMaxLength(16);
        builder.Property(c => c.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(c => c.DefaultCurrency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.TaxId).HasMaxLength(64);
        builder.Property(c => c.RegistrationNumber).HasMaxLength(64);

        // Customer-specific tax classification — owned single-valued VO inlined into the
        // contacts table. Defaults to TaxStatus.Standard (all flags false) on materialisation.
        builder.OwnsOne(c => c.TaxStatus, ts =>
        {
            ts.Property(t => t.IsExempt).IsRequired().HasDefaultValue(false);
            ts.Property(t => t.ReverseCharge).IsRequired().HasDefaultValue(false);
            ts.Property(t => t.Vatin).HasMaxLength(30);
            ts.Property(t => t.EvidenceBlobId);
        });
        builder.Property(c => c.Status).IsRequired();
        builder.Property(c => c.Roles).IsRequired();
        builder.Property(c => c.UserId);
        builder.Property(c => c.AvatarBlobId);

        // ParentContactId is a SingleValueObject<Guid> — declare explicitly as a scalar.
        builder.Property(c => c.ParentContactId);

        // Child collections use HasMany (not OwnsMany) so they can be queried directly via
        // their own DbSet — required by the reconciliation logic in EfContactStore.UpdateAsync.
        builder.HasMany(c => c.Addresses).WithOne().HasForeignKey("ContactId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Emails).WithOne().HasForeignKey("ContactId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Phones).WithOne().HasForeignKey("ContactId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.ExternalMappings).WithOne().HasForeignKey("ContactId").OnDelete(DeleteBehavior.Cascade);

        // Tenant-scoped + role-filtered listings; UserId reverse-lookup; status pages.
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.Roles);
        builder.HasIndex(c => c.UserId);
    }
}

internal sealed class ContactAddressConfiguration : IEntityTypeConfiguration<ContactAddress>
{
    public void Configure(EntityTypeBuilder<ContactAddress> builder)
    {
        builder.ToTable(
            GranitContactsDbProperties.DbTablePrefix + "addresses",
            GranitContactsDbProperties.DbSchema);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).IsRequired();
        builder.Property(a => a.IsDefault).IsRequired();
        builder.Property(a => a.Label).HasMaxLength(128);

        builder.OwnsOne(a => a.Value, v =>
        {
            v.Property(p => p.CompanyName).HasMaxLength(200);
            v.Property(p => p.Line1).HasMaxLength(200).IsRequired();
            v.Property(p => p.Line2).HasMaxLength(200);
            v.Property(p => p.City).HasMaxLength(100).IsRequired();
            v.Property(p => p.PostalCode).HasMaxLength(20).IsRequired();
            v.Property(p => p.State).HasMaxLength(100);
            v.Property(p => p.Country).HasMaxLength(2).IsRequired();
        });
    }
}

internal sealed class ContactEmailConfiguration : IEntityTypeConfiguration<ContactEmail>
{
    public void Configure(EntityTypeBuilder<ContactEmail> builder)
    {
        builder.ToTable(
            GranitContactsDbProperties.DbTablePrefix + "emails",
            GranitContactsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Address).HasMaxLength(320).IsRequired();
        builder.Property(e => e.IsPrimary).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(64);

        builder.HasIndex("ContactId", nameof(ContactEmail.IsPrimary));
    }
}

internal sealed class ContactPhoneConfiguration : IEntityTypeConfiguration<ContactPhone>
{
    public void Configure(EntityTypeBuilder<ContactPhone> builder)
    {
        builder.ToTable(
            GranitContactsDbProperties.DbTablePrefix + "phones",
            GranitContactsDbProperties.DbSchema);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Kind).IsRequired();
        builder.Property(p => p.Number).HasMaxLength(64).IsRequired();
        builder.Property(p => p.IsPrimary).IsRequired();
        builder.Property(p => p.Label).HasMaxLength(64);

        builder.HasIndex("ContactId", nameof(ContactPhone.IsPrimary));
    }
}

internal sealed class ContactExternalMappingConfiguration : IEntityTypeConfiguration<ContactExternalMapping>
{
    public void Configure(EntityTypeBuilder<ContactExternalMapping> builder)
    {
        builder.ToTable(
            GranitContactsDbProperties.DbTablePrefix + "external_mappings",
            GranitContactsDbProperties.DbSchema);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(m => m.ExternalId).HasMaxLength(256).IsRequired();

        // One mapping per (Contact, ProviderName).
        builder.HasIndex("ContactId", nameof(ContactExternalMapping.ProviderName)).IsUnique();
    }
}
