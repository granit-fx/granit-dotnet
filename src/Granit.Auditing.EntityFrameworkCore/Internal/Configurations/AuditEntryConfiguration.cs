using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditEntry"/>.
/// Table: <c>audit_log_log_entries</c>.
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "log_entries",
            GranitAuditingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Timestamp)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.UserName)
            .HasMaxLength(256);

        builder.Property(e => e.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(256);

        // Indexes for common query patterns.
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}log_entries_timestamp");

        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}log_entries_user_timestamp");

        builder.HasIndex(e => new { e.TenantId, e.Timestamp })
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}log_entries_tenant_timestamp");

        builder.HasMany(e => e.EntityChanges)
            .WithOne()
            .HasForeignKey(ec => ec.AuditEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
