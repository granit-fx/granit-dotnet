using System;
using System.Collections.Generic;
using Granit.Documents.AssetMetadata.Domain;

namespace Granit.Documents.AssetMetadata.Endpoints.Dtos;

/// <summary>
/// HTTP shape of a single <see cref="DocumentAssetMetadata"/> aggregate.
/// Mirrors the indexed-projection model: every well-known typed column is
/// surfaced inline, and the full extractor payload sits in
/// <see cref="RawMetadata"/> under <c>{extractor}:{tag}</c> keys.
/// </summary>
public sealed record AssetMetadataResponse(
    Guid Id,
    Guid DocumentId,
    Guid DocumentVersionId,
    string SourceContentType,
    AssetMetadataStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    int ExtractorCount,
    int? Width,
    int? Height,
    string? CameraMake,
    string? CameraModel,
    string? LensModel,
    int? Iso,
    double? FNumber,
    double? ExposureTimeMs,
    DateTimeOffset? TakenAt,
    double? GpsLatitude,
    double? GpsLongitude,
    double? GpsAltitude,
    int? PageCount,
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Producer,
    int? Revision,
    string? LastModifiedBy,
    long? DurationMs,
    string? Codec,
    int? Bitrate,
    string? Artist,
    string? Album,
    int? TrackNumber,
    string? Genre,
    IReadOnlyDictionary<string, string?> RawMetadata);
