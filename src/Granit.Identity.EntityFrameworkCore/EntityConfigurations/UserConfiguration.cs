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

        // Email index — point lookups via IUserLookupService (case-insensitive
        // search lives at the consumer, but the index supports the underlying
        // equality probe). Not unique — Odoo-style: multiple Users may legitimately
        // share an email (corporate inbox shared across role accounts).
        builder.HasIndex(u => u.Email)
            .HasDatabaseName($"ix_{GranitIdentityDbProperties.DbTablePrefix}users_email");

        builder.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();    // RFC 5321 max local + @ + domain
        builder.Property(u => u.FirstName).HasMaxLength(128);
        builder.Property(u => u.LastName).HasMaxLength(128);
        builder.Property(u => u.PhoneNumber).HasMaxLength(32);            // E.164 max
        builder.Property(u => u.PreferredLocale).HasMaxLength(20);        // BCP-47 worst case
        builder.Property(u => u.Timezone).HasMaxLength(64);               // IANA tz worst case
        builder.Property(u => u.IsEnabled).IsRequired();

        // Audit columns inherited from AuditedAggregateRoot — already configured
        // by ApplyGranitConventions (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
        // dimensions, plus IConcurrencyAware token). Nothing to declare here.
    }
}
