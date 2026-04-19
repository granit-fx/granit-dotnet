using System.Text.Json;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        builder.Property(e => e.State)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.RequestedAt)
            .IsRequired();

        builder.Property(e => e.ArchiveBlobReferenceId)
            .HasMaxLength(200);

        ValueConverter<List<string>, string> missingProvidersConverter = new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        ValueComparer<List<string>> missingProvidersComparer = new(
            (l, r) => (l ?? new List<string>()).SequenceEqual(r ?? new List<string>()),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property(e => e.MissingProviders)
            .HasConversion(missingProvidersConverter, missingProvidersComparer)
            .IsRequired();

        // User timeline lookup (GET /privacy/exports → GetByUserAsync).
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.RequestedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}export_requests_user_timeline");
    }
}
