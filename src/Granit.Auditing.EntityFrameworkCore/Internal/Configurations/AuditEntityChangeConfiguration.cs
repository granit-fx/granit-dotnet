using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditEntityChange"/>.
/// Table: <c>audit_log_entity_changes</c>.
/// </summary>
internal sealed class AuditEntityChangeConfiguration : IEntityTypeConfiguration<AuditEntityChange>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntityChange> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "entity_changes",
            GranitAuditingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AuditEntryId)
            .HasColumnName("AuditLogEntryId");

        builder.Property(e => e.EntityType)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ChangeType)
            .HasConversion<string>()
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
