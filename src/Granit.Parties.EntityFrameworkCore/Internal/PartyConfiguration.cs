using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Parties.EntityFrameworkCore.Internal;

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "parties",
            GranitPartiesDbProperties.DbSchema);

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

        // Free-form metadata (Stripe-style customer.metadata) — JSON column.
        // MetadataSyncInterceptor handles the IHasMetadata write side automatically.
        // HasMaxLength(4000) matches Granit.Catalog.Product convention; portable across
        // SQL Server (nvarchar) and PostgreSQL (varchar/text).
        builder.Property(c => c.MetadataJson).HasMaxLength(4000);

        // Internal notes — long free-form text bounded for DoS protection.
        builder.Property(c => c.InternalNotes).HasMaxLength(Party.MaxInternalNotesLength);

        // ParentContactId is a SingleValueObject<Guid> — declare explicitly as a scalar.
        builder.Property(c => c.ParentContactId);

        // Child collections use HasMany (not OwnsMany) so they can be queried directly via
        // their own DbSet — required by the reconciliation logic in EfPartyStore.UpdateAsync.
        builder.HasMany(c => c.Addresses).WithOne().HasForeignKey("PartyId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Emails).WithOne().HasForeignKey("PartyId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Phones).WithOne().HasForeignKey("PartyId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.ExternalMappings).WithOne().HasForeignKey("PartyId").OnDelete(DeleteBehavior.Cascade);

        // Tenant-scoped + role-filtered listings; UserId reverse-lookup; status pages.
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.Roles);
        builder.HasIndex(c => c.UserId);

        // Tier-1 deterministic duplicate detection (Epic #1280): TaxId is overwritten in
        // place with its canonical (separator-stripped, upper-case) form by the
        // PartyCanonicalisationInterceptor. Non-unique because two distinct parties may
        // legitimately share a VAT (sole proprietor + their company, group subsidiaries).
        builder.HasIndex(c => c.TaxId);
    }
}

internal sealed class PartyAddressConfiguration : IEntityTypeConfiguration<PartyAddress>
{
    public void Configure(EntityTypeBuilder<PartyAddress> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "addresses",
            GranitPartiesDbProperties.DbSchema);

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

internal sealed class PartyEmailConfiguration : IEntityTypeConfiguration<PartyEmail>
{
    public void Configure(EntityTypeBuilder<PartyEmail> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "emails",
            GranitPartiesDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Address).HasMaxLength(320).IsRequired();
        builder.Property(e => e.CanonicalEmail).HasMaxLength(320);
        builder.Property(e => e.IsPrimary).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(64);

        builder.HasIndex("PartyId", nameof(PartyEmail.IsPrimary));

        // Tier-1 deterministic duplicate detection (Epic #1280): non-unique because two
        // legitimately distinct parties may share an email (household, family). Tenant
        // filtering happens via JOIN on the parent Party in the dedup engine.
        builder.HasIndex(e => e.CanonicalEmail);
    }
}

internal sealed class PartyPhoneConfiguration : IEntityTypeConfiguration<PartyPhone>
{
    public void Configure(EntityTypeBuilder<PartyPhone> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "phones",
            GranitPartiesDbProperties.DbSchema);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Kind).IsRequired();
        builder.Property(p => p.Number).HasMaxLength(64).IsRequired();
        builder.Property(p => p.CanonicalNumber).HasMaxLength(20);
        builder.Property(p => p.IsPrimary).IsRequired();
        builder.Property(p => p.Label).HasMaxLength(64);

        builder.HasIndex("PartyId", nameof(PartyPhone.IsPrimary));

        // Tier-1 deterministic duplicate detection (Epic #1280): non-unique because two
        // legitimately distinct parties may share a phone (family, shared landline).
        builder.HasIndex(p => p.CanonicalNumber);
    }
}

internal sealed class PartyExternalMappingConfiguration : IEntityTypeConfiguration<PartyExternalMapping>
{
    public void Configure(EntityTypeBuilder<PartyExternalMapping> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "external_mappings",
            GranitPartiesDbProperties.DbSchema);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(m => m.ExternalId).HasMaxLength(256).IsRequired();

        // One mapping per (Party, ProviderName).
        builder.HasIndex("PartyId", nameof(PartyExternalMapping.ProviderName)).IsUnique();
    }
}

internal sealed class PartyDuplicateCandidateConfiguration : IEntityTypeConfiguration<PartyDuplicateCandidate>
{
    public void Configure(EntityTypeBuilder<PartyDuplicateCandidate> builder)
    {
        builder.ToTable(
            GranitPartiesDbProperties.DbTablePrefix + "duplicate_candidates",
            GranitPartiesDbProperties.DbSchema);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId);
        builder.Property(c => c.PartyId).IsRequired();
        builder.Property(c => c.CandidateId).IsRequired();
        builder.Property(c => c.Tier).IsRequired();
        builder.Property(c => c.Score).HasPrecision(5, 4).IsRequired();
        builder.Property(c => c.SignalsJson).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.DismissedAt);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        // Pair uniqueness is enforced by storing the pair already ordered
        // (PartyId < CandidateId — see PartyDuplicateCandidate.Create). The unique key
        // covers tenant + ordered pair + tier so the same pair can be surfaced from
        // multiple tiers (Tier-1 deterministic vs Tier-3 fuzzy) without colliding.
        builder.HasIndex(c => new { c.TenantId, c.PartyId, c.CandidateId, c.Tier }).IsUnique();

        // Admin "list pending duplicates" query: filters by tenant + non-dismissed,
        // orders by score / updatedAt. The compound index serves the WHERE clause.
        builder.HasIndex(c => new { c.TenantId, c.DismissedAt });
    }
}
