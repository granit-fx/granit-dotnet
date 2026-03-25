using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditPropertyChange"/>.
/// Table: <c>audit_log_property_changes</c>.
/// </summary>
internal sealed class AuditPropertyChangeConfiguration : IEntityTypeConfiguration<AuditPropertyChange>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditPropertyChange> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "property_changes",
            GranitAuditingDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PropertyName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.OriginalValue)
            .HasColumnType("text");

        builder.Property(e => e.NewValue)
            .HasColumnType("text");
    }
}
