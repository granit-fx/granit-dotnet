using Granit.AuditLog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditEntityChange"/>.
/// Table: <c>audit_entity_changes</c>.
/// </summary>
internal sealed class AuditEntityChangeConfiguration : IEntityTypeConfiguration<AuditEntityChange>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntityChange> builder)
    {
        builder.ToTable("audit_entity_changes");

        builder.HasKey(e => e.Id);

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

        builder.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("ix_audit_entity_changes_type_id");

        builder.HasMany(e => e.PropertyChanges)
            .WithOne()
            .HasForeignKey(pc => pc.AuditEntityChangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
