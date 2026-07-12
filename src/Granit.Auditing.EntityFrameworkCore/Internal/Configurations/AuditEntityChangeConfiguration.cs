using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditEntityChange"/>.
/// Table: <c>audit_log_entity_changes</c>.
/// </summary>
/// <param name="excludeFromMigrations">
/// <c>true</c> when a secondary audited context maps the table without owning its DDL
/// (exactly one context may emit the audit-table migrations).
/// </param>
internal sealed class AuditEntityChangeConfiguration(bool excludeFromMigrations = false) : IEntityTypeConfiguration<AuditEntityChange>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntityChange> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "entity_changes",
            GranitAuditingDbProperties.DbSchema,
            t => t.ExcludeFromMigrations(excludeFromMigrations));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AuditEntryId)
            .HasColumnName("AuditLogEntryId");

        builder.Property(e => e.TenantId);

        builder.Property(e => e.EntityType)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ChangeType)
            .HasMaxLength(20)
            .IsRequired();

        // Covering index for the EXISTS subquery in GetByEntityAsync
        // (EntityType, EntityId) for filtering + AuditEntryId for the semi-join.
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.AuditEntryId })
            .HasDatabaseName($"ix_{GranitAuditingDbProperties.DbTablePrefix}entity_changes_type_id");

        builder.HasMany(e => e.PropertyChanges)
            .WithOne()
            .HasForeignKey(pc => pc.AuditEntityChangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
