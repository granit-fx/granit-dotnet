using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Privacy.EntityFrameworkCore.DataExport.Internal;

internal sealed class ExportRequestEntityConfiguration : IEntityTypeConfiguration<ExportRequestEntity>
{
    public void Configure(EntityTypeBuilder<ExportRequestEntity> builder)
    {
        builder.ToTable(
            GranitPrivacyDbProperties.DbTablePrefix + "export_requests",
            GranitPrivacyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        // Nullable — pre-existing rows have no caller distinct from the subject;
        // EF reads them as null and the read paths treat null as "caller == subject".
        builder.Property(e => e.CallerUserId);

        builder.Property(e => e.State)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.RequestedAt)
            .IsRequired();

        builder.Property(e => e.ArchiveBlobReferenceId)
            .HasMaxLength(200);

        builder.Property(e => e.MissingProviders)
            .HasJsonConversion()
            .IsRequired();

        // User timeline lookup (GET /privacy/exports → GetByUserAsync).
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.RequestedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}export_requests_user_timeline");

        // Caller timeline lookup — surfaces admin-DSR rows on the operator's listing
        // ("exports I triggered"). Partial-style filter on the nullable column keeps
        // the index narrow for the self-service-only workload where Caller is null.
        builder.HasIndex(e => new { e.TenantId, e.CallerUserId, e.RequestedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}export_requests_caller_timeline");
    }
}
