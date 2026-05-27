using Granit.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Identity.EntityFrameworkCore.EntityConfigurations;

/// <summary>
/// EF Core configuration for the canonical <see cref="User"/> aggregate
/// (ADR-051). Maps the profile-only columns to the
/// <c>granit_identity_users</c> table; auth secrets and security counters
/// live on the <c>LocalIdentity</c> sibling and are configured by
/// <c>Granit.Identity.Local</c> in B-step 2.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitIdentityDbProperties.DbTablePrefix + "users",
            GranitIdentityDbProperties.DbSchema);

        builder.HasKey(u => u.Id);

        // TenantId index — multi-tenant queries lean on it for the
        // ApplyGranitConventions filter (`WHERE TenantId = currentTenant.Id`).
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName($"ix_{GranitIdentityDbProperties.DbTablePrefix}users_tenant_id");

        // EmailHash index — point lookups via IUserLookupService keyed on the
        // peppered HMAC digest, since the Email column itself is encrypted at rest
        // (random-IV AES) and cannot serve `WHERE col = ?` equality probes. Not
        // unique — Odoo-style: multiple Users may legitimately share an email
        // (corporate inbox shared across role accounts).
        builder.HasIndex(u => u.EmailHash)
            .HasDatabaseName($"ix_{GranitIdentityDbProperties.DbTablePrefix}users_email_hash");

        builder.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();

        // Email + PhoneNumber are encrypted at rest. Ciphertext is longer than
        // plaintext (AES + base64 overhead); 4096 holds the RFC-5321 email max
        // (320 chars) and any plausible international phone format.
        builder.Property(u => u.Email).HasMaxLength(4096).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(4096);

        // *Hash columns are HMAC-SHA256 digests (32 bytes → 64 hex chars). NOT a
        // secret and NOT a plain hash (peppered HMAC). Indexed for the email
        // lookup; phone hash is currently unindexed (no exact-match lookup yet).
        builder.Property(u => u.EmailHash).HasMaxLength(64);
        builder.Property(u => u.PhoneNumberHash).HasMaxLength(64);

        builder.Property(u => u.FirstName).HasMaxLength(128);
        builder.Property(u => u.LastName).HasMaxLength(128);
        builder.Property(u => u.PreferredLocale).HasMaxLength(20);        // BCP-47 worst case
        builder.Property(u => u.Timezone).HasMaxLength(64);               // IANA tz worst case
        builder.Property(u => u.IsEnabled).IsRequired();

        // Audit columns inherited from AuditedAggregateRoot — already configured
        // by ApplyGranitConventions (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
        // dimensions). User does not implement IConcurrencyAware; auth concurrency
        // is handled by LocalIdentity via ASP.NET Identity's own ConcurrencyStamp.
        // Nothing to declare here.
    }
}
