using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AuditPropertyChange"/>.
/// Table: <c>audit_log_property_changes</c>.
/// </summary>
/// <param name="excludeFromMigrations">
/// <c>true</c> when a secondary audited context maps the table without owning its DDL
/// (exactly one context may emit the audit-table migrations).
/// </param>
internal sealed class AuditPropertyChangeConfiguration(bool excludeFromMigrations = false) : IEntityTypeConfiguration<AuditPropertyChange>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditPropertyChange> builder)
    {
        builder.ToTable(
            GranitAuditingDbProperties.DbTablePrefix + "property_changes",
            GranitAuditingDbProperties.DbSchema,
            t => t.ExcludeFromMigrations(excludeFromMigrations));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PropertyName)
            .HasMaxLength(256)
            .IsRequired();

        // No explicit column type: the provider picks its native unbounded string type
        // (PostgreSQL "text", SQL Server "nvarchar(max)" — the legacy SQL Server "text"
        // LOB type breaks most operators).
        builder.Property(e => e.OriginalValue);

        builder.Property(e => e.NewValue);
    }
}
