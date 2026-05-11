using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Endpoints.Dtos;

namespace Granit.Documents.AssetMetadata.Endpoints.Mapping;

/// <summary>Mapping helpers between <see cref="DocumentAssetMetadata"/> and HTTP DTOs.</summary>
internal static class AssetMetadataMappingExtensions
{
    internal static AssetMetadataResponse ToResponse(this DocumentAssetMetadata m) =>
        new(
            Id: m.Id,
            DocumentId: m.DocumentId,
            DocumentVersionId: m.DocumentVersionId,
            SourceContentType: m.SourceContentType,
            Status: m.Status,
            CreatedAt: m.CreatedAt,
            CompletedAt: m.CompletedAt,
            FailureReason: m.FailureReason,
            ExtractorCount: m.ExtractorCount,
            Width: m.Width,
            Height: m.Height,
            CameraMake: m.CameraMake,
            CameraModel: m.CameraModel,
            LensModel: m.LensModel,
            Iso: m.Iso,
            FNumber: m.FNumber,
            ExposureTimeMs: m.ExposureTimeMs,
            TakenAt: m.TakenAt,
            GpsLatitude: m.GpsLatitude,
            GpsLongitude: m.GpsLongitude,
            GpsAltitude: m.GpsAltitude,
            PageCount: m.PageCount,
            Title: m.Title,
            Author: m.Author,
            Subject: m.Subject,
            Keywords: m.Keywords,
            Producer: m.Producer,
            Revision: m.Revision,
            LastModifiedBy: m.LastModifiedBy,
            DurationMs: m.DurationMs,
            Codec: m.Codec,
            Bitrate: m.Bitrate,
            Artist: m.Artist,
            Album: m.Album,
            TrackNumber: m.TrackNumber,
            Genre: m.Genre,
            RawMetadata: m.RawMetadata);
}
