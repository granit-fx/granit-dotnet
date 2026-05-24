using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="MigrationProgress"/>.
/// Table: <c>data_migration_progress</c> (system schema, never affected by tenant schema switches).
/// </summary>
internal sealed class MigrationProgressConfiguration : IEntityTypeConfiguration<MigrationProgress>
{
    public void Configure(EntityTypeBuilder<MigrationProgress> builder)
    {
        builder.ToTable("data_migration_progress", Granit.Persistence.EntityFrameworkCore.GranitDbDefaults.HostDbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CycleId)
            .HasMaxLength(200)
            .IsRequired();

        // Persist Phase/Status as varchar to stay in sync with the host DbContext, which
        // applies ApplyGranitConventions() and stores enums as their PascalCase name. This
        // internal DbContext deliberately skips ApplyGranitConventions (no soft-delete /
        // audit filters on a system table), so the conversion must be configured here
        // explicitly — otherwise the runtime model treats the columns as int and emits
        // "WHERE Status = 0", which fails against the varchar schema (PG 42883).
        builder.Property(e => e.Phase)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ProcessedRows)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(e => e.TotalRows);

        builder.Property(e => e.LastCursor)
            .HasMaxLength(2000);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.Error)
            .HasMaxLength(4000);

        builder.Property(e => e.StartedAt);

        builder.Property(e => e.CompletedAt);

        // A cycle runs once per tenant; (CycleId, TenantId) must be unique.
        // NULL TenantId is valid for single-tenant applications.
        builder.HasIndex(e => new { e.CycleId, e.TenantId })
            .IsUnique()
            .HasDatabaseName("uq_data_migration_progress");
    }
}
