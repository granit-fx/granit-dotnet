using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditEntry"/>.
/// Table: <c>audit_log_log_entries</c>.
/// </summary>
/// <param name="excludeFromMigrations">
/// <c>true</c> when a secondary audited context maps the table without owning its DDL
/// (exactly one context may emit the audit-table migrations).
/// </param>
internal sealed class AuditEntryConfiguration(bool excludeFromMigrations = false) : IEntityTypeConfiguration<AuditEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "log_entries",
            GranitAuditingDbProperties.DbSchema,
            t => t.ExcludeFromMigrations(excludeFromMigrations));

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

        // Retention purge filters on (Category, Timestamp < cutoff) — without this index
        // every cleanup batch scans the table.
        builder.HasIndex(e => new { e.Category, e.Timestamp })
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}log_entries_category_timestamp");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}log_entries_correlation");

        builder.HasMany(e => e.EntityChanges)
            .WithOne()
            .HasForeignKey(ec => ec.AuditEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
