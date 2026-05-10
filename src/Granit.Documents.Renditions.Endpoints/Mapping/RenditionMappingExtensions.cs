using System.Collections.Generic;
using System.Linq;
using Granit.BlobStorage;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Endpoints.Dtos;

namespace Granit.Documents.Renditions.Endpoints.Mapping;

/// <summary>Mapping helpers between <c>DocumentRendition</c> and HTTP DTOs.</summary>
internal static class RenditionMappingExtensions
{
    internal static RenditionResponse ToResponse(this DocumentRendition rendition) =>
        new(
            Id: rendition.Id,
            DocumentId: rendition.DocumentId,
            DocumentVersionId: rendition.DocumentVersionId,
            Type: rendition.Type,
            Format: rendition.Format,
            Status: rendition.Status,
            SizeBytes: rendition.SizeBytes,
            Width: rendition.Width,
            Height: rendition.Height,
            CreatedAt: rendition.CreatedAt,
            CompletedAt: rendition.CompletedAt,
            FailureReason: rendition.FailureReason);

    internal static ListRenditionsResponse ToListResponse(
        this IReadOnlyList<DocumentRendition> rows, System.Guid documentId, System.Guid versionId) =>
        new(documentId, versionId, [.. rows.Select(r => r.ToResponse())]);

    internal static RenditionDownloadUrlResponse ToResponse(this PresignedDownloadUrl url) =>
        new(url.Url, url.ExpiresAt);
}
