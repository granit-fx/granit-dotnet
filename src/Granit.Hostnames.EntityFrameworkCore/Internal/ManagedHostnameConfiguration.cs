using Granit.Hostnames.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Hostnames.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="ManagedHostname"/>.
/// Table: <c>hostname_managed_hostnames</c>.
/// </summary>
internal sealed class ManagedHostnameConfiguration : IEntityTypeConfiguration<ManagedHostname>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ManagedHostname> builder)
    {
        builder.ToTable(
            GranitHostnamesDbProperties.DbTablePrefix + "managed_hostnames",
            GranitHostnamesDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // FQDN max 253 chars (RFC 1035); the SVO value converter is applied automatically
        // by ApplyGranitConventions — no HasConversion<string>() needed.
        builder.Property(e => e.Host)
            .HasMaxLength(253)
            .IsRequired();

        builder.Property(e => e.OwnerType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.OwnerId)
            .IsRequired();

        builder.Property(e => e.TenantId);

        builder.Property(e => e.IsPrimary)
            .IsRequired();

        // Persisted as varchar by the Granit enum convention; explicit max length
        // accounts for the longest current value ("Verifying" = 9 chars + headroom).
        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ConcurrencyStamp)
            .HasMaxLength(40)
            .IsRequired()
            .IsConcurrencyToken();

        // The host is globally unique across all tenants — anti-hijacking: a second
        // owner cannot claim an already-registered host.
        builder.HasIndex(e => e.Host)
            .IsUnique()
            .HasDatabaseName(
                $"uq_{GranitHostnamesDbProperties.DbTablePrefix}managed_hostnames_host");

        // Query pattern: list all hostnames for a given owner resource.
        builder.HasIndex(e => new { e.OwnerType, e.OwnerId })
            .HasDatabaseName(
                $"ix_{GranitHostnamesDbProperties.DbTablePrefix}managed_hostnames_owner");
    }
}
