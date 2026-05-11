using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Granit.Documents.AssetMetadata.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentAssetMetadata"/>.
/// Table: <c>documents_asset_metadata</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DocumentAssetMetadata.RawMetadata"/> is round-tripped as a JSON
/// string via a value converter. The column type stays portable (default
/// <c>text</c> on PostgreSQL, <c>TEXT</c> on SQLite, <c>nvarchar(max)</c> on
/// SQL Server). Postgres-hosted apps that want JSONB query support
/// (<c>raw_metadata @&gt; '{"exif:Make": "Canon"}'::jsonb</c>) can
/// <c>ALTER COLUMN raw_metadata TYPE jsonb USING raw_metadata::jsonb</c> in
/// their first migration — the framework ships no migrations per the documents
/// convention.
/// </para>
/// </remarks>
internal sealed class DocumentAssetMetadataConfiguration : IEntityTypeConfiguration<DocumentAssetMetadata>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private static readonly ValueConverter<IReadOnlyDictionary<string, string?>, string> RawConverter =
        new(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => Deserialize(v));

    private static readonly ValueComparer<IReadOnlyDictionary<string, string?>> RawComparer =
        new(
            (a, b) => ReferenceEquals(a, b) ||
                (a != null && b != null && a.Count == b.Count && a.SequenceEqual(b, KeyValuePairComparer.Instance)),
            v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
            v => new Dictionary<string, string?>(v, System.StringComparer.Ordinal));

    public void Configure(EntityTypeBuilder<DocumentAssetMetadata> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAssetMetadataDbProperties.DbTablePrefix + "asset_metadata",
            GranitAssetMetadataDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);
        builder.Property(e => e.DocumentId).IsRequired();
        builder.Property(e => e.DocumentVersionId).IsRequired();

        builder.Property(e => e.SourceContentType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.ExtractorCount).IsRequired();

        builder.Property(e => e.FailureReason).HasMaxLength(2048);

        // Image / video typed projection.
        builder.Property(e => e.Width);
        builder.Property(e => e.Height);
        builder.Property(e => e.CameraMake).HasMaxLength(128);
        builder.Property(e => e.CameraModel).HasMaxLength(128);
        builder.Property(e => e.LensModel).HasMaxLength(128);
        builder.Property(e => e.Iso);
        builder.Property(e => e.FNumber);
        builder.Property(e => e.ExposureTimeMs);
        builder.Property(e => e.TakenAt);
        builder.Property(e => e.GpsLatitude);
        builder.Property(e => e.GpsLongitude);
        builder.Property(e => e.GpsAltitude);

        // Document typed projection.
        builder.Property(e => e.PageCount);
        builder.Property(e => e.Title).HasMaxLength(512);
        builder.Property(e => e.Author).HasMaxLength(256);
        builder.Property(e => e.Subject).HasMaxLength(512);
        builder.Property(e => e.Keywords).HasMaxLength(1024);
        builder.Property(e => e.Producer).HasMaxLength(256);
        builder.Property(e => e.Revision);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(256);

        // Audio / video typed projection.
        builder.Property(e => e.DurationMs);
        builder.Property(e => e.Codec).HasMaxLength(64);
        builder.Property(e => e.Bitrate);
        builder.Property(e => e.Artist).HasMaxLength(256);
        builder.Property(e => e.Album).HasMaxLength(256);
        builder.Property(e => e.TrackNumber);
        builder.Property(e => e.Genre).HasMaxLength(64);

        builder.Property(e => e.RawMetadata)
            .HasColumnName("raw_metadata")
            .HasConversion(RawConverter, RawComparer)
            .IsRequired();

        // One row per version — re-runs after a failure UPSERT through the existing row.
        builder.HasIndex(e => e.DocumentVersionId)
            .IsUnique()
            .HasDatabaseName($"ux_{GranitAssetMetadataDbProperties.DbTablePrefix}asset_metadata_version");

        // Cascade-on-delete lookup.
        builder.HasIndex(e => e.DocumentId)
            .HasDatabaseName($"ix_{GranitAssetMetadataDbProperties.DbTablePrefix}asset_metadata_document");
    }

    private static Dictionary<string, string?> Deserialize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, string?>(System.StringComparer.Ordinal);
        }
        return JsonSerializer.Deserialize<Dictionary<string, string?>>(raw, JsonOptions)
            ?? new Dictionary<string, string?>(System.StringComparer.Ordinal);
    }

    private sealed class KeyValuePairComparer : IEqualityComparer<KeyValuePair<string, string?>>
    {
        public static readonly KeyValuePairComparer Instance = new();

        public bool Equals(KeyValuePair<string, string?> x, KeyValuePair<string, string?> y) =>
            string.Equals(x.Key, y.Key, System.StringComparison.Ordinal)
            && string.Equals(x.Value, y.Value, System.StringComparison.Ordinal);

        public int GetHashCode(KeyValuePair<string, string?> obj) =>
            HashCode.Combine(obj.Key, obj.Value);
    }
}
