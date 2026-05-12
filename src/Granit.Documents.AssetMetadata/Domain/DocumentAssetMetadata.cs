using System.Collections.Generic;
using Granit.Documents.AssetMetadata.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.AssetMetadata.Domain;

/// <summary>
/// Aggregate root holding extracted metadata for a single <c>DocumentVersion</c>.
/// One row per version; idempotently re-runnable on retry.
/// </summary>
/// <remarks>
/// <para>
/// Storage pattern: <i>indexed projection + raw archive</i>. Well-known fields
/// (camera, dimensions, GPS, page count, audio track …) lift to typed columns
/// so SQL queries can filter on them; the full extractor payload is preserved
/// verbatim in <see cref="RawMetadata"/> for audit / forensics. Mirrors how
/// Cloudinary, Bynder, and Adobe AEM Assets shape their metadata store.
/// </para>
/// <para>
/// Each registered <c>IAssetMetadataExtractor</c> projects what it understands
/// into the typed columns (first-write wins — image and video MAY both supply
/// dimensions; the first extractor's value sticks) and appends its raw output
/// to <see cref="RawMetadata"/> under a key prefixed with its provider name.
/// </para>
/// </remarks>
public sealed class DocumentAssetMetadata : AggregateRoot, IMultiTenant
{
    /// <summary>EF Core materialisation constructor.</summary>
    private DocumentAssetMetadata() { }

    /// <summary>
    /// Creates a <see cref="AssetMetadataStatus.Pending"/> row before any extractor runs.
    /// </summary>
    public static DocumentAssetMetadata Create(
        Guid id,
        Guid? tenantId,
        Guid documentId,
        Guid documentVersionId,
        string sourceContentType,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);
        return new DocumentAssetMetadata
        {
            Id = id,
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            SourceContentType = sourceContentType,
            Status = AssetMetadataStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <inheritdoc cref="IMultiTenant.TenantId" />
    public Guid? TenantId { get; private set; }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Parent document.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Version this metadata projects from (1:1 with the row).</summary>
    public Guid DocumentVersionId { get; private set; }

    /// <summary>Source content-type at extraction time.</summary>
    public string SourceContentType { get; private set; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public AssetMetadataStatus Status { get; private set; }

    /// <summary>UTC instant the row was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC instant of the last status transition to <see cref="AssetMetadataStatus.Ready"/> or <see cref="AssetMetadataStatus.Failed"/>.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Terminal failure reason; <c>null</c> when not in <see cref="AssetMetadataStatus.Failed"/>.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Number of extractors that contributed to <see cref="RawMetadata"/>.</summary>
    public int ExtractorCount { get; private set; }

    // ----- Image / video typed projection ----------------------------------

    /// <summary>Pixel width (image / video).</summary>
    public int? Width { get; private set; }

    /// <summary>Pixel height (image / video).</summary>
    public int? Height { get; private set; }

    /// <summary>Camera manufacturer (image EXIF).</summary>
    public string? CameraMake { get; private set; }

    /// <summary>Camera model (image EXIF).</summary>
    public string? CameraModel { get; private set; }

    /// <summary>Lens model (image EXIF).</summary>
    public string? LensModel { get; private set; }

    /// <summary>ISO sensitivity (image EXIF).</summary>
    public int? Iso { get; private set; }

    /// <summary>F-number / aperture (image EXIF).</summary>
    public double? FNumber { get; private set; }

    /// <summary>Exposure time in milliseconds (image EXIF).</summary>
    public double? ExposureTimeMs { get; private set; }

    /// <summary>Original capture instant (image EXIF / audio recording date).</summary>
    public DateTimeOffset? TakenAt { get; private set; }

    /// <summary>GPS latitude in decimal degrees (subject to <c>StripGpsOnUpload</c>).</summary>
    public double? GpsLatitude { get; private set; }

    /// <summary>GPS longitude in decimal degrees (subject to <c>StripGpsOnUpload</c>).</summary>
    public double? GpsLongitude { get; private set; }

    /// <summary>GPS altitude in metres (subject to <c>StripGpsOnUpload</c>).</summary>
    public double? GpsAltitude { get; private set; }

    // ----- Document typed projection ---------------------------------------

    /// <summary>Page count (PDF, paginated office).</summary>
    public int? PageCount { get; private set; }

    /// <summary>Document title (PDF / office / audio).</summary>
    public string? Title { get; private set; }

    /// <summary>Author / creator.</summary>
    public string? Author { get; private set; }

    /// <summary>Subject.</summary>
    public string? Subject { get; private set; }

    /// <summary>Keywords (comma-joined).</summary>
    public string? Keywords { get; private set; }

    /// <summary>Producer (PDF Producer / office Application).</summary>
    public string? Producer { get; private set; }

    /// <summary>Revision number (office).</summary>
    public int? Revision { get; private set; }

    /// <summary>Last modifier (office).</summary>
    public string? LastModifiedBy { get; private set; }

    // ----- Audio / video typed projection ----------------------------------

    /// <summary>Duration in milliseconds (audio / video).</summary>
    public long? DurationMs { get; private set; }

    /// <summary>Codec name (audio / video).</summary>
    public string? Codec { get; private set; }

    /// <summary>Bitrate (bits per second).</summary>
    public int? Bitrate { get; private set; }

    /// <summary>Artist (audio).</summary>
    public string? Artist { get; private set; }

    /// <summary>Album (audio).</summary>
    public string? Album { get; private set; }

    /// <summary>Track number (audio).</summary>
    public int? TrackNumber { get; private set; }

    /// <summary>Genre (audio).</summary>
    public string? Genre { get; private set; }

    // ----- Raw archive ------------------------------------------------------

    /// <summary>
    /// Verbatim extractor payload — keyed by <c>{providerName}:{tag}</c>, values
    /// are the extractor-native form serialised as a string. Persisted as
    /// <c>jsonb</c> on Postgres, <c>nvarchar(max)</c> elsewhere.
    /// </summary>
    public IReadOnlyDictionary<string, string?> RawMetadata { get; private set; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Transitions to <see cref="AssetMetadataStatus.Extracting"/>.</summary>
    public void MarkExtracting()
    {
        if (Status is not AssetMetadataStatus.Pending and not AssetMetadataStatus.Failed)
        {
            throw new InvalidOperationException(
                $"AssetMetadata {Id} cannot start extracting from status {Status}.");
        }
        Status = AssetMetadataStatus.Extracting;
        FailureReason = null;
    }

    /// <summary>
    /// Applies a single extractor's result — first-write wins on typed columns,
    /// raw payload is appended under the extractor's prefix.
    /// </summary>
    public void ApplyExtraction(AssetMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Width ??= result.Width;
        Height ??= result.Height;
        CameraMake ??= result.CameraMake;
        CameraModel ??= result.CameraModel;
        LensModel ??= result.LensModel;
        Iso ??= result.Iso;
        FNumber ??= result.FNumber;
        ExposureTimeMs ??= result.ExposureTimeMs;
        TakenAt ??= result.TakenAt;
        GpsLatitude ??= result.GpsLatitude;
        GpsLongitude ??= result.GpsLongitude;
        GpsAltitude ??= result.GpsAltitude;
        PageCount ??= result.PageCount;
        Title ??= result.Title;
        Author ??= result.Author;
        Subject ??= result.Subject;
        Keywords ??= result.Keywords;
        Producer ??= result.Producer;
        Revision ??= result.Revision;
        LastModifiedBy ??= result.LastModifiedBy;
        DurationMs ??= result.DurationMs;
        Codec ??= result.Codec;
        Bitrate ??= result.Bitrate;
        Artist ??= result.Artist;
        Album ??= result.Album;
        TrackNumber ??= result.TrackNumber;
        Genre ??= result.Genre;

        var merged = new Dictionary<string, string?>(RawMetadata, StringComparer.Ordinal);
        string prefix = $"{result.ExtractorName}:";
        foreach ((string key, string? value) in result.RawMetadata)
        {
            merged[prefix + key] = value;
        }
        RawMetadata = merged;
        ExtractorCount++;
    }

    /// <summary>
    /// Strips GPS columns and any GPS-prefixed RawMetadata entries (called by the
    /// upload-time GPS scrub when the bytes themselves have been cleaned).
    /// </summary>
    public void StripGps()
    {
        GpsLatitude = null;
        GpsLongitude = null;
        GpsAltitude = null;
        if (RawMetadata.Count == 0)
        {
            return;
        }
        var trimmed = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string key, string? value) in RawMetadata)
        {
            if (key.Contains(":gps", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            trimmed[key] = value;
        }
        RawMetadata = trimmed;
    }

    /// <summary>
    /// Drops PII-bearing typed columns (<see cref="Author"/>, <see cref="Artist"/>,
    /// <see cref="LastModifiedBy"/>) and removes any <see cref="RawMetadata"/>
    /// keys whose tag matches the curated personal-data substring allow-list
    /// (called by the pipeline when <c>StripPersonalDataOnUpload</c> is enabled).
    /// Returns the number of raw keys removed so the caller can record the
    /// outcome on the metrics counter.
    /// </summary>
    public int StripPersonalData()
    {
        Author = null;
        Artist = null;
        LastModifiedBy = null;
        if (RawMetadata.Count == 0)
        {
            return 0;
        }
        var trimmed = new Dictionary<string, string?>(StringComparer.Ordinal);
        int removed = 0;
        foreach ((string key, string? value) in RawMetadata)
        {
            if (Internal.PersonalDataKeyMatcher.IsPersonalData(key))
            {
                removed++;
                continue;
            }
            trimmed[key] = value;
        }
        if (removed > 0)
        {
            RawMetadata = trimmed;
        }
        return removed;
    }

    /// <summary>Transitions to <see cref="AssetMetadataStatus.Ready"/> and emits <see cref="AssetMetadataExtractedEvent"/>.</summary>
    public void MarkReady(DateTimeOffset now)
    {
        if (Status != AssetMetadataStatus.Extracting)
        {
            throw new InvalidOperationException(
                $"AssetMetadata {Id} cannot transition to Ready from status {Status}.");
        }
        Status = AssetMetadataStatus.Ready;
        CompletedAt = now;
        FailureReason = null;
        AddDomainEvent(new AssetMetadataExtractedEvent(
            Id, TenantId, DocumentId, DocumentVersionId, SourceContentType, ExtractorCount, now));
    }

    /// <summary>Records a terminal failure.</summary>
    public void MarkFailed(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = AssetMetadataStatus.Failed;
        FailureReason = reason;
        CompletedAt = now;
        AddDomainEvent(new AssetMetadataFailedEvent(
            Id, TenantId, DocumentId, DocumentVersionId, SourceContentType, reason, now));
    }
}
