using System.Collections.Generic;

namespace Granit.Documents.AssetMetadata;

/// <summary>
/// Output of a single <c>IAssetMetadataExtractor</c> run. The pipeline merges
/// every extractor's <see cref="AssetMetadataResult"/> into the
/// <c>DocumentAssetMetadata</c> aggregate.
/// </summary>
/// <param name="ExtractorName">Stable provider name (used as the prefix in <see cref="RawMetadata"/>).</param>
/// <param name="RawMetadata">Verbatim extractor output — keyed by extractor-native tag.</param>
public sealed record AssetMetadataResult(
    string ExtractorName,
    IReadOnlyDictionary<string, string?> RawMetadata)
{
    /// <summary>Pixel width (image / video).</summary>
    public int? Width { get; init; }

    /// <summary>Pixel height (image / video).</summary>
    public int? Height { get; init; }

    /// <summary>Camera manufacturer (EXIF).</summary>
    public string? CameraMake { get; init; }

    /// <summary>Camera model (EXIF).</summary>
    public string? CameraModel { get; init; }

    /// <summary>Lens model (EXIF).</summary>
    public string? LensModel { get; init; }

    /// <summary>ISO sensitivity.</summary>
    public int? Iso { get; init; }

    /// <summary>F-number / aperture.</summary>
    public double? FNumber { get; init; }

    /// <summary>Exposure time in milliseconds.</summary>
    public double? ExposureTimeMs { get; init; }

    /// <summary>Original capture instant (EXIF DateTimeOriginal, audio recording date).</summary>
    public DateTimeOffset? TakenAt { get; init; }

    /// <summary>GPS latitude in decimal degrees.</summary>
    public double? GpsLatitude { get; init; }

    /// <summary>GPS longitude in decimal degrees.</summary>
    public double? GpsLongitude { get; init; }

    /// <summary>GPS altitude in metres.</summary>
    public double? GpsAltitude { get; init; }

    /// <summary>Page count (PDF, office).</summary>
    public int? PageCount { get; init; }

    /// <summary>Document title.</summary>
    public string? Title { get; init; }

    /// <summary>Document author.</summary>
    public string? Author { get; init; }

    /// <summary>Document subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Keywords (comma-joined).</summary>
    public string? Keywords { get; init; }

    /// <summary>Producer / generator (PDF Producer, office Application).</summary>
    public string? Producer { get; init; }

    /// <summary>Revision number (office).</summary>
    public int? Revision { get; init; }

    /// <summary>Last modifier (office).</summary>
    public string? LastModifiedBy { get; init; }

    /// <summary>Duration in milliseconds (audio / video).</summary>
    public long? DurationMs { get; init; }

    /// <summary>Codec name.</summary>
    public string? Codec { get; init; }

    /// <summary>Bitrate (bits per second).</summary>
    public int? Bitrate { get; init; }

    /// <summary>Artist (audio).</summary>
    public string? Artist { get; init; }

    /// <summary>Album (audio).</summary>
    public string? Album { get; init; }

    /// <summary>Track number (audio).</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Genre (audio).</summary>
    public string? Genre { get; init; }
}
